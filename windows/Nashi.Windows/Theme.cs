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
        Dark=appearance=="dark"||(appearance=="system"&&Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value&&value==0);
        foreach(var (key,light,dark) in new[]{("Paper","#F5F5F7","#16171A"),("Ink","#1D1D1F","#F5F5F7"),("Surface","#FFFFFF","#222327"),("Inset","#F0F0F2","#191A1E"),("Muted","#6E6E73","#A1A1A6"),("Line","#D2D2D7","#38393F"),("Sage","#F2EAD7","#302B20"),("Sand","#F4EBDD","#27231B")})
            Application.Current.Resources[key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dark?dark:light));
        Application.Current.Resources["GlassControl"]=new SolidColorBrush(Dark?Color.FromArgb(0x34,0xC7,0xCD,0xDB):Color.FromArgb(0x30,0x73,0x7B,0x91));
        Application.Current.Resources["GlassControlHover"]=new SolidColorBrush(Dark?Color.FromArgb(0x38,0xFF,0xFF,0xFF):Color.FromArgb(0x2C,0x5D,0x68,0x80));
        Application.Current.Resources["GlassEdge"]=new SolidColorBrush(Dark?Color.FromArgb(0x38,0xFF,0xFF,0xFF):Color.FromArgb(0x50,0xFF,0xFF,0xFF));
        Application.Current.Resources["GlassEdgeStrong"]=new SolidColorBrush(Dark?Color.FromArgb(0x70,0xFF,0xFF,0xFF):Color.FromArgb(0x90,0xFF,0xFF,0xFF));
        Application.Current.Resources["GlassInset"]=new SolidColorBrush(Dark?Color.FromArgb(0x30,0xFF,0xFF,0xFF):Color.FromArgb(0x22,0x52,0x5C,0x70));
        Application.Current.Resources["GlassTrack"]=new SolidColorBrush(Dark?Color.FromArgb(0x55,0xFF,0xFF,0xFF):Color.FromArgb(0x45,0x43,0x4C,0x5D));
        var panel=new LinearGradientBrush();panel.StartPoint=new(0,0);panel.EndPoint=new(0,1);panel.GradientStops.Add(new(Dark?Color.FromRgb(0x1C,0x1D,0x23):Color.FromRgb(0xF8,0xF9,0xFC),0));panel.GradientStops.Add(new(Dark?Color.FromRgb(0x0B,0x0C,0x10):Color.FromRgb(0xE4,0xE7,0xEE),1));Application.Current.Resources["GlassPanel"]=panel;
        var card=new LinearGradientBrush();card.StartPoint=new(0,0);card.EndPoint=new(1,1);card.GradientStops.Add(new(Dark?Color.FromArgb(0x42,0xB9,0xC3,0xD8):Color.FromArgb(0xB2,0xFF,0xFF,0xFF),0));card.GradientStops.Add(new(Dark?Color.FromArgb(0x24,0x80,0x88,0x9A):Color.FromArgb(0x88,0xEB,0xEE,0xF4),1));Application.Current.Resources["GlassCard"]=card;
        var gold=accent switch{"blue"=>Color.FromRgb(91,156,255),"violet"=>Color.FromRgb(174,125,255),"green"=>Color.FromRgb(82,190,139),_=>Color.FromRgb(211,180,107)};
        Application.Current.Resources["Gold"]=new SolidColorBrush(gold);
        Application.Current.Resources["GlassSelected"]=new SolidColorBrush(Color.FromArgb(0x58,gold.R,gold.G,gold.B));
        Application.Current.Resources[SystemColors.HighlightBrushKey]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dark?"#554323":"#F2EAD7"));
        Application.Current.Resources[SystemColors.HighlightTextBrushKey]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(Dark?"#F5F5F7":"#1D1D1F"));
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
    }
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
