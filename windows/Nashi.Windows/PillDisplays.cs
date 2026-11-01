using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Nashi.Windows;

internal sealed record PillDisplay(string Id,int Left,int Top,int Right,int Bottom,uint Dpi,bool Primary)
{
    internal int Width=>Right-Left;
}

internal static class PillDisplays
{
    [StructLayout(LayoutKind.Sequential)]private struct Rect{internal int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct MonitorInfo
    {
        internal int Size;internal Rect Monitor,Work;internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]internal string Device;
    }
    private delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("shcore.dll")]private static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);

    internal static IReadOnlyList<PillDisplay> Enumerate()
    {
        var displays=new List<PillDisplay>();
        EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr handle,IntPtr dc,ref Rect bounds,IntPtr data)=>
        {
            var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>(),Device=string.Empty};if(!GetMonitorInfo(handle,ref info))return true;
            var dpi=96u;try{if(GetDpiForMonitor(handle,0,out var actual,out _)==0&&actual>0)dpi=actual;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            displays.Add(new(info.Device,info.Monitor.Left,info.Monitor.Top,info.Monitor.Right,info.Monitor.Bottom,dpi,(info.Flags&1)!=0));return true;
        },IntPtr.Zero);
        return displays;
    }
    internal static PillDisplay? Resolve(IReadOnlyList<PillDisplay> displays,string mode,string id)=>
        mode=="specific"?displays.FirstOrDefault(d=>string.Equals(d.Id,id,StringComparison.OrdinalIgnoreCase))??displays.FirstOrDefault(d=>d.Primary)??displays.FirstOrDefault():displays.FirstOrDefault(d=>d.Primary)??displays.FirstOrDefault();

    internal static (int X,int Y) Position(PillDisplay display,double widthDip,string position)
    {
        var width=Math.Min(display.Width,Math.Max(1,(int)Math.Round(widthDip*display.Dpi/96d)));var margin=(int)Math.Round(12*display.Dpi/96d);
        var offset=position switch{"left"=>margin,"right"=>display.Width-width-margin,_=>(display.Width-width)/2};
        return (display.Left+Math.Clamp(offset,0,Math.Max(0,display.Width-width)),display.Top);
    }
    internal static PillEdgeRegion Edge(PillDisplay display,double widthDip,string position,int logicalHeight)
    {
        var (x,y)=Position(display,widthDip,position);var width=Math.Min(display.Width,Math.Max(1,(int)Math.Round(widthDip*display.Dpi/96d)));var height=Math.Max(1,(int)Math.Round(logicalHeight*display.Dpi/96d));return new(display.Id,x,y,x+width,y+height);
    }
    internal static bool Place(Window window,PillDisplay display,double widthDip,string position)
    {
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return false;
        var (x,y)=Position(display,widthDip,position);return SetWindowPos(handle,IntPtr.Zero,x,y,0,0,0x0001|0x0004|0x0010);
    }
}
