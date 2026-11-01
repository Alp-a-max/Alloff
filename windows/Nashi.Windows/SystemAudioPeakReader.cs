using System.Runtime.InteropServices;

namespace Nashi.Windows;

// Reads only the default output endpoint's peak level. It does not record or store audio.
internal sealed class SystemAudioPeakReader : IDisposable
{
    [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]private class DeviceEnumeratorCom{}
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDeviceEnumerator
    {
        [PreserveSig]int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
        [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
    }
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevice
    {
        [PreserveSig]int Activate(ref Guid iid,uint clsctx,IntPtr activation,[MarshalAs(UnmanagedType.IUnknown)]out object value);
    }
    [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IMeter
    {
        [PreserveSig]int GetPeakValue(out float peak);
    }
    private readonly Action<float> changed;private CancellationTokenSource? lifetime;private int successfulSamples;
    internal int SuccessfulSamples=>Volatile.Read(ref successfulSamples);
    internal SystemAudioPeakReader(Action<float> changed)=>this.changed=changed;
    internal void Start()
    {
        if(lifetime is not null)return;var cts=new CancellationTokenSource();lifetime=cts;
        _=Task.Run(()=>Run(cts.Token)).ContinueWith(_=>cts.Dispose(),TaskScheduler.Default);
    }
    internal void Stop(){var old=lifetime;lifetime=null;if(old is not null)try{old.Cancel();}catch(ObjectDisposedException){}}
    private async Task Run(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            object? enumerator=null,device=null,meter=null;
            try
            {
                enumerator=new DeviceEnumeratorCom();var source=(IDeviceEnumerator)enumerator;Marshal.ThrowExceptionForHR(source.GetDefaultAudioEndpoint(0,1,out var endpoint));device=endpoint;
                var iid=typeof(IMeter).GUID;Marshal.ThrowExceptionForHR(endpoint.Activate(ref iid,23,IntPtr.Zero,out var activated));meter=activated;var peak=(IMeter)meter;
                // Reopen occasionally so switching the default output device takes effect.
                for(var i=0;i<100&&!token.IsCancellationRequested;i++)
                {
                    Marshal.ThrowExceptionForHR(peak.GetPeakValue(out var value));Interlocked.Increment(ref successfulSamples);if(!token.IsCancellationRequested)changed(Math.Clamp(value,0,1));await Task.Delay(100,token);
                }
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){break;}
            catch(Exception error)when(error is COMException or InvalidCastException or UnauthorizedAccessException){if(!token.IsCancellationRequested){changed(0);try{await Task.Delay(2000,token);}catch(OperationCanceledException){break;}}}
            finally{if(meter is not null&&Marshal.IsComObject(meter))Marshal.FinalReleaseComObject(meter);if(device is not null&&Marshal.IsComObject(device))Marshal.FinalReleaseComObject(device);if(enumerator is not null&&Marshal.IsComObject(enumerator))Marshal.FinalReleaseComObject(enumerator);}
        }
    }
    public void Dispose()=>Stop();
}
