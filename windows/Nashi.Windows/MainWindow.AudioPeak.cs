using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Nashi.Windows;

public sealed partial class MainWindow
{
    private SystemAudioPeakReader? pillPeakReader;private StackPanel? islandMediaVisualizer;private bool pillPeakRunning;private int pillPeakPending;private float pillPeakValue,pillPeakSmoothed;
    private void RefreshPillAudioMeter()
    {
        if(App.IsSmokeTest)return;
        var shown=IsVisible&&(!islandExpanded||islandCurrentWidgetTab=="media")||pillSatellites.Values.Any(window=>window.IsVisible);
        var needed=pillModeEnabled&&!islandHiddenByAutomation&&shown&&islandMediaPlaying&&preferences.Value.Animations&&SystemParameters.ClientAreaAnimation;
        if(needed==pillPeakRunning)return;pillPeakRunning=needed;
        if(needed){pillPeakReader??=new SystemAudioPeakReader(OnPillAudioPeak);pillPeakReader.Start();}
        else{pillPeakReader?.Stop();pillPeakSmoothed=0;ApplyPillAudioPeak(0);}
    }
    private void OnPillAudioPeak(float value)
    {
        Volatile.Write(ref pillPeakValue,value);if(Interlocked.Exchange(ref pillPeakPending,1)!=0||Dispatcher.HasShutdownStarted)return;
        try{Dispatcher.BeginInvoke(()=>{Interlocked.Exchange(ref pillPeakPending,0);if(!pillPeakRunning)return;var sample=Volatile.Read(ref pillPeakValue);pillPeakSmoothed=.65f*pillPeakSmoothed+.35f*sample;ApplyPillAudioPeak(pillPeakSmoothed);},DispatcherPriority.Background);}catch(InvalidOperationException){Interlocked.Exchange(ref pillPeakPending,0);}
    }
    private void ApplyPillAudioPeak(float value)
    {
        ApplyPeakToBars(pillEqualizer,value);ApplyPeakToBars(islandMediaVisualizer,value);foreach(var satellite in pillSatellites.Values)satellite.SetAudioLevel(value);
    }
    internal static void ApplyPeakToBars(StackPanel? panel,float value)
    {
        if(panel is null)return;var index=0;var amplitude=Math.Sqrt(Math.Clamp(value,0,1));
        foreach(var bar in panel.Children.OfType<Rectangle>())
        {
            if(bar.RenderTransform is not ScaleTransform transform||transform.IsFrozen){transform=new ScaleTransform(1,1);bar.RenderTransform=transform;bar.RenderTransformOrigin=new Point(.5,1);}
            var factor=(index++%3) switch{0=>.65,1=>1d,_=>.8};var target=Math.Clamp(.22+amplitude*factor,.22,1);if(Math.Abs(transform.ScaleY-target)>.015)transform.ScaleY=target;
        }
    }
    private void StopPillAudioMeter(){pillPeakRunning=false;pillPeakReader?.Dispose();pillPeakReader=null;}
}
