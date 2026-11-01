using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Nashi.Windows;

// A secondary screen has its own compact surface; the shared workspace stays in MainWindow.
internal sealed class PillSatelliteWindow : Window
{
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]private static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    private readonly Image artwork=new(){Width=18,Height=18,Stretch=Stretch.UniformToFill};
    private readonly StackPanel bars=new(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
    private readonly DispatcherTimer intent=new();private readonly Action<string,bool> open;private PillDisplay display;private string position;private double scale;private bool hoverEnabled;
    internal string DisplayId=>display.Id;

    internal PillSatelliteWindow(PillDisplay display,string position,double scale,int hoverDelay,bool hoverEnabled,Action<string,bool> open)
    {
        this.display=display;this.position=position;this.scale=scale;this.hoverEnabled=hoverEnabled;this.open=open;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.Manual;
        Width=PillGeometry.CollapsedWidth*scale;Height=PillGeometry.CollapsedHeight*scale;intent.Interval=TimeSpan.FromMilliseconds(hoverDelay);
        var row=new DockPanel{Margin=new(10,0,10,0)};artwork.Source=FileGarden.Artwork("NashiMark");artwork.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(artwork,Dock.Left);row.Children.Add(artwork);
        foreach(var height in new[]{5d,11d,7d})bars.Children.Add(new Rectangle{Width=2.5,Height=height,RadiusX=1.25,RadiusY=1.25,Fill=Brushes.White,Margin=new(1,0,1,0),VerticalAlignment=VerticalAlignment.Center});bars.Visibility=Visibility.Collapsed;row.Children.Add(bars);
        var surface=new Border{Background=Theme.Brush("GlassPanel"),BorderBrush=Theme.Brush("GlassEdge"),BorderThickness=new(1),CornerRadius=new(16),Child=row,Cursor=Cursors.Hand};AutomationProperties.SetName(surface,"Nashi");Content=surface;
        SourceInitialized+=(_,_)=>{var handle=new WindowInteropHelper(this).Handle;var style=GetWindowLongPtr(handle,-20).ToInt64();SetWindowLongPtr(handle,-20,new IntPtr(style|0x08000000L|0x80L));Place();};
        intent.Tick+=(_,_)=>{intent.Stop();if(hoverEnabled&&IsVisible&&surface.IsMouseOver)open(display.Id,false);};
        surface.MouseEnter+=(_,_)=>{if(!hoverEnabled)return;intent.Stop();intent.Start();};surface.MouseLeave+=(_,_)=>intent.Stop();
        surface.PreviewMouseLeftButtonDown+=(_,e)=>{intent.Stop();open(display.Id,true);e.Handled=true;};
        Closed+=(_,_)=>intent.Stop();
    }
    internal void Update(PillDisplay monitor,string newPosition,double newScale,int hoverDelay,bool hover,ImageSource? cover,bool playing)
    {
        var moved=display!=monitor||position!=newPosition||scale!=newScale;display=monitor;position=newPosition;scale=newScale;hoverEnabled=hover;intent.Interval=TimeSpan.FromMilliseconds(hoverDelay);
        var width=PillGeometry.CollapsedWidth*scale;var height=PillGeometry.CollapsedHeight*scale;if(Width!=width)Width=width;if(Height!=height)Height=height;
        SetMedia(cover,playing);if(moved)Place();
    }
    internal void SetMedia(ImageSource? cover,bool playing){artwork.Source=cover??FileGarden.Artwork("NashiMark");bars.Visibility=playing?Visibility.Visible:Visibility.Collapsed;}
    internal void SetAudioLevel(float value)=>MainWindow.ApplyPeakToBars(bars,value);
    private void Place(){PillDisplays.Place(this,display,Width,position);}
}
