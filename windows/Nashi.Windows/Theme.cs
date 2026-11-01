using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Nashi.Windows;

internal static class Theme
{
    internal static bool Dark{get;private set;}
    internal static Brush Primary=>Brush("Gold");
    internal static Brush Brush(string name)=>(Brush)Application.Current.FindResource(name);
    internal static void Apply(string appearance,string accent="gold")
    {
        Dark=true;
        foreach(var (key,value) in new[]{("Paper",GlassDesign.Background),("Ink",GlassDesign.Ink),("Surface",GlassDesign.Surface),("Inset",GlassDesign.Inset),("Muted",GlassDesign.Muted),("Line",GlassDesign.Line),("Sage",GlassDesign.Surface),("Sand",GlassDesign.Inset),("Gold",GlassDesign.Ink),("GlassPanel",GlassDesign.Background)})
            Application.Current.Resources[key]=GlassDesign.Solid(value);
        foreach(var (key,alpha) in new[]{("GlassControl",GlassDesign.ControlAlpha),("GlassControlHover",GlassDesign.HoverAlpha),("GlassEdge",GlassDesign.EdgeAlpha),("GlassEdgeStrong",GlassDesign.StrongEdgeAlpha),("GlassInset",GlassDesign.InsetAlpha),("GlassTrack",GlassDesign.TrackAlpha),("GlassCard",GlassDesign.CardAlpha),("GlassSelected",GlassDesign.SelectionAlpha)})
            Application.Current.Resources[key]=new SolidColorBrush(Color.FromArgb(alpha,255,255,255));
        Application.Current.Resources[SystemColors.HighlightBrushKey]=GlassDesign.Solid("#454545");
        Application.Current.Resources[SystemColors.HighlightTextBrushKey]=GlassDesign.Solid(GlassDesign.Ink);
        foreach(Window window in Application.Current.Windows)ApplyTitleBar(window);
    }
    internal static void Attach(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty,"Paper");window.SetResourceReference(Window.ForegroundProperty,"Ink");
        window.Loaded+=(_,_)=>{if(window.WindowStyle!=WindowStyle.None)DesktopGeometry.FitToWorkArea(window);L10n.Bind(window,Window.TitleProperty,window.Title);L10n.Attach(window);};
        window.SourceInitialized+=(_,_)=>ApplyTitleBar(window);
    }
    internal static void Entrance(UIElement element)
    {
        if(!WheelPresentation.Preferences.Animations||!SystemParameters.ClientAreaAnimation)return;
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)));
        var offset=new TranslateTransform();element.RenderTransform=offset;
        offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(8,0,TimeSpan.FromMilliseconds(180)){DecelerationRatio=.8});
    }
    private static void ApplyTitleBar(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;var dark=Dark?1:0;_ = DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
        // Layered WPF windows cannot reliably use DWM backdrops; those retain the glass fallback.
        if(!window.AllowsTransparency&&OperatingSystem.IsWindowsVersionAtLeast(10,0,22621)){var mica=2;_ = DwmSetWindowAttribute(handle,38,ref mica,sizeof(int));}
    }
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
