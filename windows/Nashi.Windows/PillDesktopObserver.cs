using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Nashi.Windows;

internal sealed record PillEdgeRegion(string DisplayId,int Left,int Top,int Right,int Bottom)
{
    internal bool Contains(int x,int y)=>x>=Left&&x<Right&&y>=Top&&y<Bottom;
}

// The hook runs on its own message thread. It never consumes input or performs UI work;
// only edge crossings and coalesced foreground changes are sent to the dispatcher.
internal sealed class PillDesktopObserver : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]private struct Point{internal int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseData{internal Point Point;internal uint Data,Flags,Time;internal UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Message{internal IntPtr Window;internal uint Id;internal UIntPtr WParam;internal IntPtr LParam;internal uint Time;internal Point Point;internal uint Private;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{internal int Left,Top,Right,Bottom;}
    private delegate IntPtr MouseCallback(int code,IntPtr message,IntPtr data);
    private delegate void WinEventCallback(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int id,MouseCallback callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")]private static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventCallback callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")]private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")]private static extern int GetMessage(out Message message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")]private static extern bool PeekMessage(out Message message,IntPtr window,uint min,uint max,uint remove);
    [DllImport("user32.dll")]private static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out Rect rect);

    private readonly Dispatcher dispatcher;private readonly Action<string?> edgeChanged;private readonly Action foregroundChanged;private readonly Action<bool> ready;
    private readonly MouseCallback mouseCallback;private readonly WinEventCallback windowCallback;private readonly Thread thread;
    private PillEdgeRegion[] regions=[];private string? edge;private int watchForeground,pendingForeground,disposed;private uint threadId;
    private IntPtr mouseHook,foregroundHook,locationHook;

    internal PillDesktopObserver(Dispatcher dispatcher,Action<string?> edgeChanged,Action foregroundChanged,Action<bool> ready)
    {
        this.dispatcher=dispatcher;this.edgeChanged=edgeChanged;this.foregroundChanged=foregroundChanged;this.ready=ready;
        mouseCallback=OnMouse;windowCallback=OnWindow;thread=new Thread(Run){IsBackground=true,Name="Nashi pill edge events"};thread.Start();
    }
    internal void Configure(PillEdgeRegion[] edgeRegions,bool watchWindows)
    {
        Volatile.Write(ref regions,edgeRegions);Volatile.Write(ref watchForeground,watchWindows?1:0);
        var id=Volatile.Read(ref threadId);if(id!=0)PostThreadMessage(id,0x8001,UIntPtr.Zero,IntPtr.Zero);
    }
    private void Run()
    {
        threadId=GetCurrentThreadId();PeekMessage(out _,IntPtr.Zero,0,0,0);
        try
        {
            mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);
            foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,windowCallback,0,0,2);
            locationHook=SetWinEventHook(0x800B,0x800B,IntPtr.Zero,windowCallback,0,0,2);
            Dispatch(()=>ready(mouseHook!=IntPtr.Zero&&foregroundHook!=IntPtr.Zero&&locationHook!=IntPtr.Zero));
            while(Volatile.Read(ref disposed)==0&&GetMessage(out var message,IntPtr.Zero,0,0)>0)
            {
                if(message.Id!=0x8001)continue;
                if(Volatile.Read(ref regions).Length==0&&mouseHook!=IntPtr.Zero){UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;if(edge is not null){edge=null;Dispatch(()=>edgeChanged(null));}}
                else if(Volatile.Read(ref regions).Length>0&&mouseHook==IntPtr.Zero){mouseHook=SetWindowsHookEx(14,mouseCallback,GetModuleHandle(null),0);if(mouseHook==IntPtr.Zero)Dispatch(()=>ready(false));}
            }
        }
        finally{if(mouseHook!=IntPtr.Zero)UnhookWindowsHookEx(mouseHook);if(foregroundHook!=IntPtr.Zero)UnhookWinEvent(foregroundHook);if(locationHook!=IntPtr.Zero)UnhookWinEvent(locationHook);}
    }
    private IntPtr OnMouse(int code,IntPtr message,IntPtr data)
    {
        try
        {
            if(code>=0&&message.ToInt64()==0x200&&Volatile.Read(ref disposed)==0)
            {
                var areas=Volatile.Read(ref regions);if(areas.Length>0)
                {
                    var point=Marshal.PtrToStructure<MouseData>(data).Point;string? next=null;
                    foreach(var region in areas)if(region.Contains(point.X,point.Y)){next=region.DisplayId;break;}
                    if(!string.Equals(edge,next,StringComparison.Ordinal)){edge=next;var value=next;Dispatch(()=>edgeChanged(value));}
                }
                else if(edge is not null){edge=null;Dispatch(()=>edgeChanged(null));}
            }
        }
        catch(Exception){/* Never interrupt a system input chain. */}
        return CallNextHookEx(mouseHook,code,message,data);
    }
    private void OnWindow(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint eventThread,uint time)
    {
        try
        {
            if(Volatile.Read(ref watchForeground)==0||Volatile.Read(ref disposed)!=0||window==IntPtr.Zero)return;
            if(kind==0x800B&&(objectId!=0||window!=GetForegroundWindow()))return;
            if(Interlocked.Exchange(ref pendingForeground,1)!=0)return;
            Dispatch(()=>{Interlocked.Exchange(ref pendingForeground,0);foregroundChanged();});
        }
        catch(Exception){Interlocked.Exchange(ref pendingForeground,0);}
    }
    private void Dispatch(Action action){if(Volatile.Read(ref disposed)!=0||dispatcher.HasShutdownStarted)return;try{dispatcher.BeginInvoke(action,DispatcherPriority.Background);}catch(InvalidOperationException){}}
    internal static bool? ForegroundOverlaps(PillEdgeRegion bounds)
    {
        var hwnd=GetForegroundWindow();if(hwnd==IntPtr.Zero||hwnd==GetShellWindow()||!IsWindowVisible(hwnd)||IsIconic(hwnd))return false;
        GetWindowThreadProcessId(hwnd,out var pid);if(pid==(uint)Environment.ProcessId)return null;
        if(!GetWindowRect(hwnd,out var rect))return false;
        return rect.Left<bounds.Right&&rect.Right>bounds.Left&&rect.Top<bounds.Bottom&&rect.Bottom>bounds.Top;
    }
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;var id=Volatile.Read(ref threadId);if(id!=0)PostThreadMessage(id,0x12,UIntPtr.Zero,IntPtr.Zero);}
}
