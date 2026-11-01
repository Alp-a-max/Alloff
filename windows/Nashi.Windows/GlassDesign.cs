using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Nashi.Windows;

// The monochrome glass palette and motion values live here for all Windows surfaces.
internal static class GlassDesign
{
    internal const string Background = "#090909", Surface = "#202020", Inset = "#151515";
    internal const string Ink = "#F6F6F6", Muted = "#A3A3A3", Line = "#3A3A3A";
    internal const byte ControlAlpha = 24, HoverAlpha = 38, EdgeAlpha = 42, StrongEdgeAlpha = 77;
    internal const byte InsetAlpha = 18, TrackAlpha = 72, CardAlpha = 22, SelectionAlpha = 46;
    internal const byte SliderTrackAlpha = 54, RangeSelectionAlpha = 56;
    internal const double SliderHoverRailScale = 1.25, SliderHoverThumbScale = 1.15, SliderPressedThumbScale = .9, SliderMotionMs = 120;
    internal const double Radius = 15, NavRadius = 17, SelectionOpacity = .18;
    internal const double DisabledOpacity = .30, InactiveOpacity = .58, PressScale = .96;
    internal const double NavMotionMs = 330, PageMotionMs = 300, PageOffset = 18, PressMotionMs = 100, ReleaseMotionMs = 240, StaggerMs = 35, CardFadeMs = 180;
    internal static bool MotionEnabled => WheelPresentation.Preferences.Animations && SystemParameters.ClientAreaAnimation;
    internal static Duration NavDuration => new(TimeSpan.FromMilliseconds(NavMotionMs));
    internal static Duration PageDuration => new(TimeSpan.FromMilliseconds(PageMotionMs));
    internal static IEasingFunction Easing => new CubicEase { EasingMode = EasingMode.EaseOut };
    internal static SolidColorBrush Solid(string value) => new((Color)ColorConverter.ConvertFromString(value));
    internal static void FadeTo(UIElement element, double value)
    {
        var previous=element.Opacity;element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=value;
        if(MotionEnabled && Math.Abs(previous-value)>.01)element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(previous,value,PageDuration){EasingFunction=Easing});
    }
}

// One selection surface moves beneath the existing buttons; buttons retain their own handlers.
internal sealed class GlassNavBar : Grid
{
    private readonly Canvas overlay = new() { IsHitTestVisible = false };
    private readonly Border capsule = new() { CornerRadius = new(GlassDesign.NavRadius), BorderThickness = new(1), Visibility = Visibility.Collapsed };
    private readonly TranslateTransform position = new();
    private readonly ScaleTransform stretch = new();
    private Button? selected;
    internal StackPanel Items { get; } = new() { Orientation = Orientation.Horizontal };

    internal GlassNavBar()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        capsule.SetResourceReference(Border.BackgroundProperty, "GlassSelected");
        capsule.SetResourceReference(Border.BorderBrushProperty, "GlassEdgeStrong");
        var transforms=new TransformGroup();transforms.Children.Add(stretch);transforms.Children.Add(position);capsule.RenderTransform=transforms;
        overlay.Children.Add(capsule); Children.Add(overlay); Children.Add(Items);
        SizeChanged += (_, _) => Position(false);
        Loaded += (_, _) => Position(false);
    }

    internal void Select(Button? button, bool animate = true)
    {
        var changed = !ReferenceEquals(selected, button);
        selected = button;
        if (!IsLoaded) return;
        Dispatcher.BeginInvoke(() => Position(changed && animate));
    }

    private void Position(bool animate)
    {
        if (selected is null || !selected.IsVisible || selected.ActualWidth <= 0) { capsule.Visibility = Visibility.Collapsed; return; }
        var point = selected.TranslatePoint(new Point(0, 0), this);
        var oldX = position.X; var oldWidth = capsule.ActualWidth;
        var wasVisible = capsule.Visibility == Visibility.Visible;
        capsule.Visibility = Visibility.Visible;
        position.BeginAnimation(TranslateTransform.XProperty, null);
        capsule.BeginAnimation(WidthProperty, null);
        position.X = point.X; position.Y = point.Y; capsule.Width = selected.ActualWidth; capsule.Height = selected.ActualHeight;stretch.CenterX=selected.ActualWidth/2;stretch.CenterY=selected.ActualHeight/2;
        if (!animate || !wasVisible || !GlassDesign.MotionEnabled || oldWidth <= 0) return;
        position.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(oldX, point.X, GlassDesign.NavDuration) { EasingFunction = GlassDesign.Easing });
        capsule.BeginAnimation(WidthProperty, new DoubleAnimation(oldWidth, selected.ActualWidth, GlassDesign.NavDuration) { EasingFunction = GlassDesign.Easing });
        var liquid=new DoubleAnimationUsingKeyFrames{Duration=GlassDesign.NavDuration};liquid.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromPercent(0)));liquid.KeyFrames.Add(new EasingDoubleKeyFrame(1+Math.Min(.08,Math.Abs(point.X-oldX)/1800),KeyTime.FromPercent(.45)){EasingFunction=GlassDesign.Easing});liquid.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromPercent(1)){EasingFunction=GlassDesign.Easing});stretch.BeginAnimation(ScaleTransform.ScaleXProperty,liquid);
    }
}

internal static class GlassPageTransition
{
    internal static void Enter(UIElement content, int direction)
    {
        if (!GlassDesign.MotionEnabled || direction == 0) return;
        var offset = new TranslateTransform(); content.RenderTransform = offset;
        offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(GlassDesign.PageOffset * direction, 0, GlassDesign.PageDuration) { EasingFunction = GlassDesign.Easing });
        content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.4, 1, GlassDesign.PageDuration) { EasingFunction = GlassDesign.Easing });
    }
    internal static void Stagger(IEnumerable<UIElement> items)
    {
        if(!GlassDesign.MotionEnabled)return;var index=0;
        foreach(var item in items.Take(8))
        {
            var delay=TimeSpan.FromMilliseconds(index++*GlassDesign.StaggerMs);var end=delay+TimeSpan.FromMilliseconds(GlassDesign.CardFadeMs);
            var fade=new DoubleAnimationUsingKeyFrames{Duration=new Duration(end)};
            fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(delay)));fade.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(end)){EasingFunction=GlassDesign.Easing});
            item.BeginAnimation(UIElement.OpacityProperty,fade);
        }
    }
}
