using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nashi.Windows;

internal enum IslandVisibilityPhase { Collapsed, Opening, Expanded, Closing }

// A transition completion may arrive after a newer request; only the newest ticket may finish.
internal sealed class IslandVisibilityMachine
{
    private long generation;
    internal IslandVisibilityPhase Phase { get; private set; } = IslandVisibilityPhase.Collapsed;
    internal long Start(bool expanded, bool animated)
    {
        generation++;
        Phase = animated ? (expanded ? IslandVisibilityPhase.Opening : IslandVisibilityPhase.Closing)
                         : (expanded ? IslandVisibilityPhase.Expanded : IslandVisibilityPhase.Collapsed);
        return generation;
    }
    internal void Complete(long ticket)
    {
        if (ticket != generation) return;
        if (Phase == IslandVisibilityPhase.Opening) Phase = IslandVisibilityPhase.Expanded;
        else if (Phase == IslandVisibilityPhase.Closing) Phase = IslandVisibilityPhase.Collapsed;
    }
}

public sealed partial class MainWindow
{
    internal const int IslandHoverIntentMilliseconds = 120;
    internal const int IslandCollapseDelayMilliseconds = 320;
    internal const int IslandTransitionMilliseconds = 180;
    private readonly IslandVisibilityMachine islandVisibility = new();
    private DispatcherTimer? islandHoverTimer, islandCollapseTimer;
    private long islandHoverDeadline, islandCollapseDeadline;
    private bool islandHoverPending, islandCollapsePending, islandCollapseAfterDrag, islandDragActive;

    private void AttachIslandPointerInteractions(FrameworkElement surface)
    {
        surface.AllowDrop = true;
        surface.MouseEnter += (_, _) =>
        {
            CancelIslandCollapse();
            if (!islandExpanded && ShouldExpandOnHover(preferences.Value.PillVisibilityMode)) ScheduleIslandHover();
        };
        surface.MouseLeave += (_, _) =>
        {
            CancelIslandHover();
            if (preferences.Value.PillVisibilityMode == "smart") ScheduleIslandCollapse();
        };
        surface.LostKeyboardFocus += (_, _) =>
        {
            if (preferences.Value.PillVisibilityMode == "smart" && !IsPointerOverIsland()) ScheduleIslandCollapse();
        };
        surface.LostMouseCapture += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (preferences.Value.PillVisibilityMode == "smart" && !IsPointerOverIsland()) ScheduleIslandCollapse();
        }, DispatcherPriority.Input);
        surface.DragEnter += (_, e) =>
        {
            if (busy || !SupportsIslandDrop(e.Data)) return;
            islandDragActive = true; CancelIslandHover(); CancelIslandCollapse();
            if (!islandExpanded) SetIslandExpanded(true);
            if (islandHint is not null) islandHint.Text = IslandText("Drop to add files", "Dosya eklemek için bırak");
            e.Handled = true;
        };
        surface.DragLeave += (_, _) => { islandDragActive = false; ScheduleIslandCollapse(afterDrag:true); };
        surface.Drop += (_, _) => { islandDragActive = false; ScheduleIslandCollapse(afterDrag:true); };
        Closed += (_, _) => { CancelIslandHover(); CancelIslandCollapse(); };
    }

    private static bool SupportsIslandDrop(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.UnicodeText) ||
        data.GetDataPresent(DataFormats.Html) || data.GetDataPresent("UniformResourceLocatorW");

    private void AttachIslandPopupInteractions(Popup popup)
    {
        if (popup.Child is not FrameworkElement child) return;
        child.MouseEnter += (_, _) => CancelIslandCollapse();
        child.MouseLeave += (_, _) => { if (preferences.Value.PillVisibilityMode == "smart") ScheduleIslandCollapse(); };
        child.LostMouseCapture += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (preferences.Value.PillVisibilityMode == "smart" && !IsPointerOverIsland() && !IsPointerOverPopup(popup)) ScheduleIslandCollapse();
        }, DispatcherPriority.Input);
        child.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (preferences.Value.PillVisibilityMode == "smart" && !IsPointerOverIsland() && !IsPointerOverPopup(popup)) ScheduleIslandCollapse();
        }, DispatcherPriority.Input);
    }
    private static bool IsPointerOverPopup(Popup? popup) => popup is { IsOpen:true } &&
        (popup.IsMouseOver || popup.IsKeyboardFocusWithin || popup.Child is UIElement child && (child.IsMouseOver || child.IsMouseCaptureWithin || child.IsKeyboardFocusWithin));

    private bool IsPointerOverIsland()
    {
        if (islandPanel is null || !IsVisible || islandHiddenByAutomation) return false;
        if (islandPanel.IsMouseOver || islandPanel.IsMouseCaptureWithin) return true;
        var point = Mouse.GetPosition(islandPanel);
        return point.X >= 0 && point.Y >= 0 && point.X < islandPanel.ActualWidth && point.Y < islandPanel.ActualHeight;
    }

    private void ScheduleIslandHover()
    {
        islandHoverPending = true; islandHoverDeadline = Environment.TickCount64 + IslandHoverIntentMilliseconds;
        islandHoverTimer ??= new DispatcherTimer(DispatcherPriority.Input);
        islandHoverTimer.Tick -= OpenAfterHover; islandHoverTimer.Tick += OpenAfterHover;
        islandHoverTimer.Stop(); islandHoverTimer.Interval = TimeSpan.FromMilliseconds(IslandHoverIntentMilliseconds); islandHoverTimer.Start();
    }
    private void CancelIslandHover() { islandHoverPending = false; islandHoverTimer?.Stop(); }
    private void OpenAfterHover(object? sender, EventArgs e)
    {
        islandHoverTimer?.Stop(); if (!islandHoverPending) return;
        var remaining = islandHoverDeadline - Environment.TickCount64;
        if (remaining > 0) { islandHoverTimer!.Interval = TimeSpan.FromMilliseconds(remaining); islandHoverTimer.Start(); return; }
        islandHoverPending = false;
        if (!islandExpanded && ShouldExpandOnHover(preferences.Value.PillVisibilityMode) && IsPointerOverIsland()) SetIslandExpanded(true);
    }

    private void ScheduleIslandCollapse(bool afterDrag = false)
    {
        if (!islandExpanded) return;
        islandCollapsePending = true; islandCollapseAfterDrag = afterDrag; islandCollapseDeadline = Environment.TickCount64 + IslandCollapseDelayMilliseconds;
        islandCollapseTimer ??= new DispatcherTimer(DispatcherPriority.Background);
        islandCollapseTimer.Tick -= CollapseIfPointerLeft; islandCollapseTimer.Tick += CollapseIfPointerLeft;
        islandCollapseTimer.Stop(); islandCollapseTimer.Interval = TimeSpan.FromMilliseconds(IslandCollapseDelayMilliseconds); islandCollapseTimer.Start();
    }
    private void CancelIslandCollapse() { islandCollapsePending = false; islandCollapseTimer?.Stop(); }
    private void CollapseIfPointerLeft(object? sender, EventArgs e)
    {
        islandCollapseTimer?.Stop(); if (!islandCollapsePending) return;
        var remaining = islandCollapseDeadline - Environment.TickCount64;
        if (remaining > 0) { islandCollapseTimer!.Interval = TimeSpan.FromMilliseconds(remaining); islandCollapseTimer.Start(); return; }
        islandCollapsePending = false;
        if (!islandCollapseAfterDrag && preferences.Value.PillVisibilityMode != "smart") return;
        if (!ShouldCollapseIsland(IsPointerOverIsland(), islandDragActive,
                IsPointerOverPopup(islandActionPopup), IsPointerOverPopup(islandMediaPopup),
                islandPanel?.IsKeyboardFocusWithin == true && Keyboard.FocusedElement is TextBoxBase)) return;
        SetIslandExpanded(false);
    }

    internal static bool ShouldCollapseIsland(bool pointerOverSurface, bool dragging,
        bool actionPopupHovered, bool mediaPopupHovered, bool editorFocused = false) =>
        !pointerOverSurface && !dragging && !actionPopupHovered && !mediaPopupHovered && !editorFocused;
    internal static bool ShouldExpandOnHover(string mode) => mode == "smart";
}
