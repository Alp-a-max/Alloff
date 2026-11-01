using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nashi.Windows;

internal static class IslandInteractionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(MainWindow.IslandHoverIntentMilliseconds == 120 && MainWindow.IslandCollapseDelayMilliseconds == 320,
            "The hover intent or collapse delay is outside the intended range.");
        var machine=new IslandVisibilityMachine();var opening=machine.Start(true,true);check(machine.Phase==IslandVisibilityPhase.Opening,"Opening phase was not entered.");
        var closing=machine.Start(false,true);machine.Complete(opening);check(machine.Phase==IslandVisibilityPhase.Closing,"An old opening completion changed a newer closing transition.");
        var reopening=machine.Start(true,true);machine.Complete(closing);check(machine.Phase==IslandVisibilityPhase.Opening,"An old closing completion changed a newer opening transition.");
        machine.Complete(reopening);check(machine.Phase==IslandVisibilityPhase.Expanded,"The latest opening transition did not finish.");
        machine.Start(false,false);check(machine.Phase==IslandVisibilityPhase.Collapsed,"A nonanimated collapse did not finish immediately.");
        check(MainWindow.ShouldCollapseIsland(false, false, false, false),
            "The island did not collapse after the pointer left all surfaces.");
        check(!MainWindow.ShouldCollapseIsland(true, false, false, false),
            "The island collapsed while the pointer was still over it.");
        check(!MainWindow.ShouldCollapseIsland(false, true, false, false),
            "The island collapsed while a file drag was active.");
        check(!MainWindow.ShouldCollapseIsland(false, false, true, false),
            "The island collapsed while its action popover was being used.");
        check(!MainWindow.ShouldCollapseIsland(false, false, false, true),
            "The island collapsed while its media popover was being used.");
        check(!MainWindow.ShouldCollapseIsland(false, false, false, false, editorFocused: true),
            "The island collapsed while a text editor inside it still had keyboard focus.");
        check(MainWindow.ShouldExpandOnHover("smart")&&!MainWindow.ShouldExpandOnHover("shortcut")&&!MainWindow.ShouldExpandOnHover("always"),
            "Only the optional Smart visibility mode should expand the pill on hover.");
        var collapsed=PillGeometry.Create(PillGeometry.CollapsedWidth,PillGeometry.CollapsedHeight);
        var expanded=PillGeometry.Create(PillGeometry.ExpandedWidth,PillGeometry.ExpandedHeight);
        check(Math.Abs(collapsed.Bounds.Width-PillGeometry.CollapsedWidth)<.01&&Math.Abs(collapsed.Bounds.Height-PillGeometry.CollapsedHeight)<.01,
            "The collapsed pill geometry does not match its layout bounds.");
        check(Math.Abs(expanded.Bounds.Width-PillGeometry.ExpandedWidth)<.01&&Math.Abs(expanded.Bounds.Height-PillGeometry.ExpandedHeight)<.01,
            "The expanded pill geometry does not match its layout bounds.");
        check(PillGeometry.TryParseHotkey("Ctrl+Alt+N",out var modifiers,out var key)&&modifiers==3&&key==0x4E,
            "The default pill hotkey could not be parsed.");
        check(!PillGeometry.TryParseHotkey("Ctrl+N",out _,out _),
            "An unsupported pill hotkey was accepted.");
        var slider=new Slider{Minimum=0,Maximum=100,Value=50};slider.SetResourceReference(FrameworkElement.StyleProperty,"GlassSlider");slider.ApplyTemplate();
        var track=slider.Template.FindName("PART_Track",slider) as Track;track?.DecreaseRepeatButton?.ApplyTemplate();track?.IncreaseRepeatButton?.ApplyTemplate();track?.Thumb?.ApplyTemplate();
        check(track?.DecreaseRepeatButton?.Template is not null&&track.IncreaseRepeatButton?.Template is not null&&track.Thumb?.Template is not null,"Slider track or thumb was left on a system template.");
        check(slider.IsMoveToPointEnabled&&slider.Focusable&&track?.DecreaseRepeatButton?.Command==Slider.DecreaseLarge&&track.IncreaseRepeatButton?.Command==Slider.IncreaseLarge,"Slider pointer or keyboard configuration changed.");
        var rail=slider.Template.FindName("Rail",slider) as Border;check(rail?.Background is SolidColorBrush b&&b.Color.R==255&&b.Color.G==255&&b.Color.B==255&&b.Color.A is >=46 and <=64,"Slider unfilled rail must use translucent neutral white.");
        var unfilled=track?.IncreaseRepeatButton?.Template?.FindName("UnfilledHitArea",track.IncreaseRepeatButton) as Border;
        check(unfilled?.Background is SolidColorBrush transparent&&transparent.Color.A==0,"The unfilled repeat button must not draw the system accent.");
        App.AnimateGlassSlider(slider,true,false);check(rail?.RenderTransform is ScaleTransform {IsFrozen:false},"Slider hover cannot animate a frozen template transform.");App.AnimateGlassSlider(slider,true,true);App.AnimateGlassSlider(slider,false,false);
        Slider.IncreaseSmall.Execute(null,slider);check(slider.Value>50,"Slider keyboard increment did not change its value.");
        var range=new RangeTimeline{Width=200,Height=54};range.Set(30,5,20);range.Measure(new Size(200,54));range.Arrange(new Rect(0,0,200,54));range.UpdateLayout();
        var bitmap=new RenderTargetBitmap(200,54,96,96,PixelFormats.Pbgra32);bitmap.Render(range);var pixels=new byte[200*54*4];bitmap.CopyPixels(pixels,800,0);
        var monochrome=true;for(var i=0;i<pixels.Length;i+=4)if(pixels[i]!=pixels[i+1]||pixels[i+1]!=pixels[i+2]){monochrome=false;break;}check(monochrome,"The range timeline must not paint colored track or handles.");
    }
}
