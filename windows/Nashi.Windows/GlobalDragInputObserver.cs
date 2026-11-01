using System.Runtime.InteropServices;
using System.Windows.Threading;
using Nashi.Core;

namespace Nashi.Windows;

internal readonly record struct DragPoint(int X,int Y);
internal readonly record struct DragStart(DragPoint Origin,DragPoint Current,string Kind);

// This model also supports pressing the configured modifier after moving the mouse.
internal sealed class DragGestureState
{
    private bool down,started;private DragPoint origin,current;
    internal bool IsDown=>down;
    internal bool NeedsModifierCheck=>down&&!started&&Math.Abs(current.X-origin.X)+Math.Abs(current.Y-origin.Y)>=10;
    internal void Begin(DragPoint point){down=true;started=false;origin=current=point;}
    internal void Move(DragPoint point){if(down)current=point;}
    internal DragStart? TryStart(string chord,Preferences settings)
    {
        if(!NeedsModifierCheck||!settings.Enabled)return null;
        var kind=chord==settings.ToolsModifier?"tools":chord==settings.ConvertModifier?"convert":chord==settings.ActionsModifier?"actions":null;
        if(kind is null)return null;started=true;return new(origin,current,kind);
    }
    internal bool End(){var result=started;down=false;started=false;return result;}
}

// The low-level callback only updates a tiny state object and always passes the event on.
// Modifier checks use an 80 ms timer only while a moved drag is held without a modifier.
internal sealed class GlobalDragInputObserver:IDisposable
{
    [StructLayout(LayoutKind.Sequential)]private struct Point{internal int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseData{internal Point Point;internal uint Data,Flags,Time;internal UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Message{internal IntPtr Window;internal uint Id;internal UIntPtr WParam;internal IntPtr LParam;internal uint Time;internal Point Point;internal uint Private;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{internal int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{internal int Size;internal Rect Monitor,Work;internal uint Flags;}
    private delegate IntPtr MouseProc(int code,IntPtr message,IntPtr data);
    private delegate void WinProc(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time);
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int id,MouseProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")]private static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")]private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")]private static extern int GetMessage(out Message message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")]private static extern bool PeekMessage(out Message message,IntPtr window,uint min,uint max,uint remove);
    [DllImport("user32.dll")]private static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern UIntPtr SetTimer(IntPtr window,UIntPtr id,uint interval,IntPtr callback);
    [DllImport("user32.dll")]private static extern bool KillTimer(IntPtr window,UIntPtr id);
    [DllImport("user32.dll")]private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);

    private readonly Dispatcher dispatcher;private readonly Action<DragStart> start;private readonly Action finish;private readonly Action<bool> availability;
    private readonly MouseProc mouseProc;private readonly WinProc foregroundProc;private readonly Thread thread;private readonly DragGestureState state=new();
    private Preferences settings;private IntPtr mouseHook,foregroundHook,locationHook;private UIntPtr modifierTimer,windowTimer;private uint threadId;private int disposed;private long lastWindowCheck;
    internal GlobalDragInputObserver(Dispatcher dispatcher,Preferences initial,Action<DragStart> start,Action finish,Action<bool> availability)
    {
        this.dispatcher=dispatcher;settings=initial;this.start=start;this.finish=finish;this.availability=availability;mouseProc=OnMouse;foregroundProc=OnForeground;
        thread=new Thread(Run){IsBackground=true,Name="Nashi drag input"};thread.Start();
    }
    internal void Configure(Preferences value){Volatile.Write(ref settings,value);var id=Volatile.Read(ref threadId);if(id!=0)PostThreadMessage(id,0x8002,UIntPtr.Zero,IntPtr.Zero);}
    private void Run()
    {
        threadId=GetCurrentThreadId();PeekMessage(out _,IntPtr.Zero,0,0,0);
        try
        {
            foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,foregroundProc,0,0,0);locationHook=SetWinEventHook(0x800B,0x800B,IntPtr.Zero,foregroundProc,0,0,0);RefreshHook();
            while(Volatile.Read(ref disposed)==0&&GetMessage(out var message,IntPtr.Zero,0,0)>0)
            {
                if(message.Id==0x8002)RefreshHook();else if(message.Id==0x113&&message.WParam==modifierTimer)CheckModifier();else if(message.Id==0x113&&message.WParam==windowTimer){StopWindowTimer();lastWindowCheck=Environment.TickCount64;RefreshHook();}
            }
        }
        finally{StopTimer();StopWindowTimer();if(mouseHook!=IntPtr.Zero)UnhookWindowsHookEx(mouseHook);if(foregroundHook!=IntPtr.Zero)UnhookWinEvent(foregroundHook);if(locationHook!=IntPtr.Zero)UnhookWinEvent(locationHook);}
    }
    private void RefreshHook()
    {
        var enabled=Volatile.Read(ref settings).Enabled;var suspended=IsForegroundFullscreen();var need=enabled&&!suspended;
        if(!need&&mouseHook!=IntPtr.Zero){UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;StopTimer();if(state.End())Dispatch(finish);}
        if(need&&mouseHook==IntPtr.Zero)mouseHook=SetWindowsHookEx(14,mouseProc,GetModuleHandle(null),0);
        var available=foregroundHook!=IntPtr.Zero&&locationHook!=IntPtr.Zero&&(!need||mouseHook!=IntPtr.Zero);
        if(!available&&mouseHook!=IntPtr.Zero){UnhookWindowsHookEx(mouseHook);mouseHook=IntPtr.Zero;StopTimer();state.End();}Dispatch(()=>availability(available));
    }
    private IntPtr OnMouse(int code,IntPtr message,IntPtr data)
    {
        try
        {
            if(code>=0&&Volatile.Read(ref disposed)==0)
            {
                var kind=message.ToInt64();
                if(kind==0x200&&state.IsDown){var point=Marshal.PtrToStructure<MouseData>(data).Point;state.Move(new(point.X,point.Y));if(state.NeedsModifierCheck&&modifierTimer==UIntPtr.Zero){CheckModifier();if(state.NeedsModifierCheck)modifierTimer=SetTimer(IntPtr.Zero,UIntPtr.Zero,80,IntPtr.Zero);}}
                else if(kind is 0x201 or 0x202 or 0x204 or 0x205)
                {
                    var swapped=GetSystemMetrics(23)!=0;var down=swapped?0x204:0x201;var up=swapped?0x205:0x202;
                    if(kind==down){StopTimer();var point=Marshal.PtrToStructure<MouseData>(data).Point;state.Begin(new(point.X,point.Y));}
                    else if(kind==up){StopTimer();if(state.End())Dispatch(finish);}
                }
            }
        }
        catch(Exception){/* Do not let a gesture observer interrupt input. */}
        return CallNextHookEx(mouseHook,code,message,data);
    }
    private void CheckModifier()
    {
        if(!state.NeedsModifierCheck){StopTimer();return;}
        var parts=new List<string>(3);if((GetAsyncKeyState(0x11)&0x8000)!=0)parts.Add("Ctrl");if((GetAsyncKeyState(0x10)&0x8000)!=0)parts.Add("Shift");if((GetAsyncKeyState(0x12)&0x8000)!=0)parts.Add("Alt");
        if(state.TryStart(string.Join('+',parts),Volatile.Read(ref settings)) is {} gesture){StopTimer();Dispatch(()=>start(gesture));}
    }
    private void OnForeground(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint eventThread,uint time)
    {
        try
        {
            if(kind==0x800B&&(objectId!=0||window!=GetForegroundWindow()))return;
            if(kind==0x800B&&Environment.TickCount64-lastWindowCheck<120){if(windowTimer==UIntPtr.Zero)windowTimer=SetTimer(IntPtr.Zero,UIntPtr.Zero,120,IntPtr.Zero);return;}
            StopWindowTimer();lastWindowCheck=Environment.TickCount64;RefreshHook();
        }
        catch(Exception){Dispatch(()=>availability(false));}
    }
    internal static bool IsForegroundFullscreen()
    {
        var window=GetForegroundWindow();if(window==IntPtr.Zero)return false;GetWindowThreadProcessId(window,out var process);if(process==(uint)Environment.ProcessId)return false;
        if(!GetWindowRect(window,out var rect))return false;var monitor=MonitorFromWindow(window,2);var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(monitor==IntPtr.Zero||!GetMonitorInfo(monitor,ref info))return false;
        var style=GetWindowLongPtr(window,-16).ToInt64();return CoversMonitor(rect.Left,rect.Top,rect.Right,rect.Bottom,info.Monitor.Left,info.Monitor.Top,info.Monitor.Right,info.Monitor.Bottom,style);
    }
    internal static bool CoversMonitor(int left,int top,int right,int bottom,int monitorLeft,int monitorTop,int monitorRight,int monitorBottom,long style)=>
        (style&0x00C00000)==0&&left<=monitorLeft&&top<=monitorTop&&right>=monitorRight&&bottom>=monitorBottom;
    private void StopTimer(){if(modifierTimer!=UIntPtr.Zero){KillTimer(IntPtr.Zero,modifierTimer);modifierTimer=UIntPtr.Zero;}}
    private void StopWindowTimer(){if(windowTimer!=UIntPtr.Zero){KillTimer(IntPtr.Zero,windowTimer);windowTimer=UIntPtr.Zero;}}
    private void Dispatch(Action action){if(Volatile.Read(ref disposed)!=0||dispatcher.HasShutdownStarted)return;try{dispatcher.BeginInvoke(action,DispatcherPriority.Background);}catch(InvalidOperationException){}}
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;var id=Volatile.Read(ref threadId);if(id!=0)PostThreadMessage(id,0x12,UIntPtr.Zero,IntPtr.Zero);}
}
