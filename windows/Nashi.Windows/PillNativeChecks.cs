using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using Nashi.Core;

namespace Nashi.Windows;

internal static class PillNativeChecks
{
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    internal static int Run(string output)
    {
        var directory=Path.GetFullPath(output);Directory.CreateDirectory(directory);var errors=new List<string>();var dispatcher=Dispatcher.CurrentDispatcher;
        void Check(bool value,string error){if(!value)errors.Add(error);}
        var store=new PreferencesStore(directory);store.Save(new Preferences());var notifications=0;
        using(var watcher=new PreferencesFileWatcher(store,dispatcher,()=>notifications++))
        {
            var external=new PreferencesStore(directory);external.Save(external.Value with{PillPosition="right",PillHoverDelayMs=220});
            Check(Wait(()=>notifications>0,2500)&&store.Value.PillPosition=="right"&&store.Value.PillHoverDelayMs==220,"External valid settings were not applied.");
            var count=notifications;File.WriteAllText(store.FilePath,"{invalid");Pump(500);Check(notifications==count&&store.Value.PillPosition=="right","Invalid settings changed the last good values.");
        }
        bool? hookReady=null;var observer=new PillDesktopObserver(dispatcher,_=>{},()=>{},value=>hookReady=value);
        Check(Wait(()=>hookReady.HasValue,2500),"The native event observer did not initialize.");
        var nativeHooks=hookReady==true;
        if(nativeHooks){observer.Configure([new PillEdgeRegion("probe",-30000,-30000,-29900,-29997)],true);Pump(150);observer.Configure([],false);Pump(150);}
        observer.Dispose();
        bool? dragObserverReady=null;using(var dragObserver=new GlobalDragInputObserver(dispatcher,new Preferences(),_=>{},()=>{},value=>dragObserverReady=value))
        {
            Check(Wait(()=>dragObserverReady.HasValue,2500),"The drag input observer did not initialize.");dragObserver.Configure(new Preferences{Enabled=false});Pump(100);
        }
        var samples=0;using(var meter=new SystemAudioPeakReader(_=>{})){meter.Start();Wait(()=>meter.SuccessfulSamples>0,2000);samples=meter.SuccessfulSamples;meter.Stop();}
        var screens=PillDisplays.Enumerate();var peekTest="not-run";bool? hoverDidNotActivate=null;var satelliteWindowCreated=false;
        if(nativeHooks&&screens.Count>0)
        {
            var stateDir=Path.Combine(directory,"visibility");Directory.CreateDirectory(stateDir);var visibilityPreferences=new PreferencesStore(stateDir);
            visibilityPreferences.Save(new Preferences{PillModeEnabled=true,PillVisibilityMode="peek",PillOnboardingCompleted=true,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false});
            var window=new MainWindow(library:new LibraryStore(stateDir),checkForUpdates:false,offerToolSetup:false);
            try
            {
                window.Show();Pump(150);window.UIProbeConfigureVisibility();
                if(window.UIProbePointerOver)peekTest="skipped-pointer-over-window";
                else
                {
                    var hidden=Wait(()=>window.UIProbeAutoHidden,2000);if(hidden){window.UIProbeEdge((screens.FirstOrDefault(s=>s.Primary)??screens[0]).Id);peekTest=Wait(()=>window.UIAuditIslandExpanded&&window.IsVisible,2000)?"passed":"failed-edge-reveal";}else peekTest="failed-auto-hide";
                    Check(peekTest=="passed","Peek did not auto-hide and reveal after simulated edge intent.");hoverDidNotActivate=GetForegroundWindow()!=new WindowInteropHelper(window).Handle;Check(hoverDidNotActivate==true,"Hover reveal activated the pill window.");
                }
            }
            finally{window.Close();Pump(50);}
            var offscreen=new PillDisplay("synthetic",-10000,-10000,-8000,-8000,96,false);var satellite=new PillSatelliteWindow(offscreen,"center",1,120,true,(_,_)=>{});
            try{satellite.Show();Pump(80);satelliteWindowCreated=satellite.IsVisible&&GetForegroundWindow()!=new WindowInteropHelper(satellite).Handle;Check(satelliteWindowCreated,"The secondary compact window could not open without activation.");}
            finally{satellite.Close();Pump(30);}
        }
        var fileDir=Path.Combine(directory,"file-interactions");Directory.CreateDirectory(fileDir);var filePreferences=new PreferencesStore(fileDir);
        filePreferences.Save(new Preferences{PillModeEnabled=true,PillVisibilityMode="shortcut",PillOnboardingCompleted=true,AutomaticUpdateChecks=false,KeepInTray=false,Feedback=false,Animations=false});
        var fileWindow=new MainWindow(library:new LibraryStore(fileDir),checkForUpdates:false,offerToolSetup:false);
        try
        {
            fileWindow.Show();fileWindow.UIAuditPresentIsland(true,"now");Pump(150);fileWindow.UpdateLayout();
            var choose=FindButton(fileWindow,"Pill.Files.Choose");Check(choose is not null&&Hits(fileWindow,choose),"Choose files is covered by another element.");
            var pickerOpened=false;var closePicker=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
            closePicker.Tick+=(_,_)=>{var picker=Application.Current.Windows.OfType<NashiFilePickerWindow>().FirstOrDefault(w=>w.IsVisible);if(picker is null)return;pickerOpened=true;closePicker.Stop();picker.Close();};
            closePicker.Start();if(choose is not null)choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));closePicker.Stop();Check(pickerOpened,"Choose files did not open the picker.");
            var fixture=Path.Combine(fileDir,"synthetic.txt");File.WriteAllText(fixture,"synthetic");fileWindow.UIAuditImportData(new DataObject(DataFormats.FileDrop,new[]{fixture}));Pump(100);fileWindow.UpdateLayout();
            Check(fileWindow.SelectedFiles.SequenceEqual(new[]{fixture}),"Pasted file payload did not populate the selection.");
            var clear=FindButton(fileWindow,"Pill.Files.Clear");Check(clear is not null&&Hits(fileWindow,clear),"Clear selection is covered by another element.");
            clear?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(fileWindow.SelectedFiles.Count==0&&File.Exists(fixture),"Clear did not remove the selection while retaining the original file.");
            fileWindow.UIAuditPresentIsland(true,"actions");Pump(80);Check(Descendants(fileWindow).OfType<TextBox>().Any(field=>AutomationProperties.GetAutomationId(field)=="Toolbox.Search"&&field.IsVisible),"Toolbox did not return after leaving Files.");
        }
        finally{fileWindow.Close();Pump(50);}
        var report=new{nativeHooks,dragObserverReady,outputMeterSamples=samples,detectedScreens=screens.Count,preferencesReloaded=notifications,peekTest,hoverDidNotActivate,satelliteWindowCreated,errors};
        File.WriteAllText(Path.Combine(directory,"native-probe.json"),JsonSerializer.Serialize(report));Console.WriteLine(JsonSerializer.Serialize(report));return errors.Count==0?0:1;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private static Button? FindButton(Window window,string id)=>Descendants(window).OfType<Button>().FirstOrDefault(button=>AutomationProperties.GetAutomationId(button)==id&&button.IsVisible);
    private static bool Hits(Window window,Button button)
    {
        var point=button.TranslatePoint(new Point(button.ActualWidth/2,button.ActualHeight/2),window);var hit=VisualTreeHelper.HitTest(window,point)?.VisualHit;
        while(hit is not null){if(ReferenceEquals(hit,button))return true;hit=VisualTreeHelper.GetParent(hit);}return false;
    }
    private static bool Wait(Func<bool> condition,int milliseconds)
    {
        var deadline=Environment.TickCount64+milliseconds;while(!condition()&&Environment.TickCount64<deadline)Pump(30);return condition();
    }
    private static void Pump(int milliseconds)
    {
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
}
