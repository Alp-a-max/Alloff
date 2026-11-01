using System.Windows;
using System.Windows.Threading;

namespace Nashi.Windows;

public sealed partial class MainWindow
{
    private PillDesktopObserver? pillDesktopObserver;
    private bool pillObserverReady,pillAutoHidden,pillEdgeHot,pillForegroundOverlap;
    private string? pillEdgeDisplayId,pillActiveDisplayId;
    private DispatcherTimer? pillMediaPeekTimer,pillForegroundTimer;
    private long pillMediaPeekDeadline;
    private int displayRefreshQueued;
    private readonly Dictionary<string,PillSatelliteWindow> pillSatellites=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,bool> pillDisplayOverlap=new(StringComparer.OrdinalIgnoreCase);
    internal bool UIProbeAutoHidden=>pillAutoHidden;
    internal bool UIProbePointerOver=>IsPointerOverIsland();
    internal void UIProbeConfigureVisibility()=>ConfigurePillVisibility();
    internal void UIProbeEdge(string? displayId)=>OnPillEdgeChanged(displayId);

    private PillDisplay? TargetPillDisplay()
    {
        var displays=PillDisplays.Enumerate();var mode=preferences.Value.PillMonitorMode;var id=mode=="all"?pillActiveDisplayId??preferences.Value.PillMonitorId:preferences.Value.PillMonitorId;
        return PillDisplays.Resolve(displays,mode=="all"&&!string.IsNullOrEmpty(id)?"specific":mode,id??"");
    }

    private void ConfigurePillVisibility()
    {
        if(!pillModeEnabled||!IsLoaded)return;
        var mode=preferences.Value.PillVisibilityMode;
        if(mode is not ("smart" or "peek"))
        {
            pillDesktopObserver?.Dispose();pillDesktopObserver=null;pillObserverReady=false;pillEdgeHot=false;pillEdgeDisplayId=null;pillMediaPeekTimer?.Stop();
            if(pillAutoHidden&&!islandHiddenByAutomation)RevealPillAutomatically(false);
            SyncPillSatellites();
            return;
        }
        if(pillDesktopObserver is null)
        {
            pillObserverReady=false;PillDesktopObserver? created=null;
            created=new(Dispatcher,OnPillEdgeChanged,QueuePillForegroundRefresh,available=>
            {
                if(!ReferenceEquals(pillDesktopObserver,created))return;pillObserverReady=available;
                if(!available){if(pillAutoHidden&&!islandHiddenByAutomation)RevealPillAutomatically(false);SyncPillSatellites();L10n.SetText(status,"Screen edge detection is unavailable.");return;}
                RefreshPillVisibility();
            });
            pillDesktopObserver=created;
        }
        RefreshPillVisibility();
    }
    private void QueuePillForegroundRefresh()
    {
        pillForegroundTimer??=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(120)};
        if(pillForegroundTimer.IsEnabled)return;pillForegroundTimer.Tick-=OnPillForegroundTick;pillForegroundTimer.Tick+=OnPillForegroundTick;pillForegroundTimer.Start();
    }
    private void OnPillForegroundTick(object? sender,EventArgs e){pillForegroundTimer?.Stop();RefreshPillVisibility();}
    private void UpdatePillObserverRegions()
    {
        if(pillDesktopObserver is null)return;var mode=preferences.Value.PillVisibilityMode;
        var displays=PillDisplays.Enumerate();var targets=preferences.Value.PillMonitorMode=="all"?displays:TargetPillDisplay() is {} selected?new[]{selected}:Array.Empty<PillDisplay>();
        var active=TargetPillDisplay()?.Id;var regions=!islandHiddenByAutomation?targets.Where(d=>string.Equals(d.Id,active,StringComparison.OrdinalIgnoreCase)?pillAutoHidden:!pillSatellites.TryGetValue(d.Id,out var satellite)||!satellite.IsVisible).Select(d=>PillDisplays.Edge(d,PillGeometry.CollapsedWidth*preferences.Value.PillScale,preferences.Value.PillPosition,3)).ToArray():Array.Empty<PillEdgeRegion>();
        pillDesktopObserver.Configure(regions,mode=="smart");
    }
    private void OnPillEdgeChanged(string? displayId)
    {
        if(islandHiddenByAutomation||islandExpanded)return;
        pillEdgeDisplayId=displayId;pillEdgeHot=displayId is not null;
        if(pillEdgeHot){CancelIslandCollapse();ScheduleIslandHover();}
        else CancelIslandHover();
    }
    private void OpenPillOnDisplay(string displayId,bool activate)
    {
        if(islandHiddenByAutomation||!DesktopGeometry.IsInteractive(this))return;pillActiveDisplayId=displayId;pillEdgeHot=false;pillEdgeDisplayId=null;pillAutoHidden=false;
        SetNoActivate(true);ShowActivated=false;if(!IsVisible)Show();PositionIsland();ResumeVisiblePolling();
        SyncPillSatellites();SetIslandExpanded(true,activate:activate);if(activate){SetNoActivate(false);if(!IsActive)Activate();}UpdatePillObserverRegions();
    }
    private void RevealPillAutomatically(bool expand)
    {
        if(islandHiddenByAutomation)return;
        if(pillEdgeDisplayId is not null&&preferences.Value.PillMonitorMode=="all")pillActiveDisplayId=pillEdgeDisplayId;
        pillEdgeHot=false;pillEdgeDisplayId=null;pillAutoHidden=false;SetNoActivate(true);ShowActivated=false;if(!IsVisible)Show();PositionIsland();ResumeVisiblePolling();
        if(expand)SetIslandExpanded(true,activate:false);SyncPillSatellites();UpdatePillObserverRegions();
    }
    private void HidePillAutomatically()
    {
        if(islandHiddenByAutomation||pillAutoHidden)return;
        CancelIslandHover();CancelIslandCollapse();if(islandExpanded)SetIslandExpanded(false,false,activate:false);
        SetNoActivate(true);pillAutoHidden=true;if(IsVisible)Hide();StopHiddenPolling();SyncPillSatellites();UpdatePillObserverRegions();
    }
    private void RefreshPillVisibility()
    {
        if(!pillModeEnabled||!IsLoaded||islandHiddenByAutomation||pillDesktopObserver is null||!pillObserverReady)return;
        var mode=preferences.Value.PillVisibilityMode;if(mode is not ("smart" or "peek"))return;
        if(mode=="smart"&&TargetPillDisplay() is {} display)
        {
            var bounds=PillDisplays.Edge(display,PillGeometry.CollapsedWidth*preferences.Value.PillScale,preferences.Value.PillPosition,(int)PillGeometry.CollapsedHeight);
            var overlap=PillDesktopObserver.ForegroundOverlaps(bounds);if(overlap.HasValue)pillForegroundOverlap=overlap.Value;
        }
        SyncPillSatellites();
        if(!DesktopGeometry.IsInteractive(this)||islandExpanded||IsPointerOverIsland()||IsPointerOverPopup(islandActionPopup)||IsPointerOverPopup(islandMediaPopup)){UpdatePillObserverRegions();return;}
        var peeking=pillMediaPeekDeadline>Environment.TickCount64;
        if(mode=="peek"&&!peeking||mode=="smart"&&pillForegroundOverlap)HidePillAutomatically();
        else if(pillAutoHidden)RevealPillAutomatically(false);
        UpdatePillObserverRegions();
    }
    private void SyncPillSatellites()
    {
        if(!pillModeEnabled||!IsLoaded||App.IsSmokeTest)return;
        if(preferences.Value.PillMonitorMode!="all"||islandHiddenByAutomation)
        {foreach(var window in pillSatellites.Values)window.Close();pillSatellites.Clear();pillDisplayOverlap.Clear();RefreshPillAudioMeter();return;}
        var displays=PillDisplays.Enumerate();var active=TargetPillDisplay()?.Id;var wanted=displays.Where(d=>!string.Equals(d.Id,active,StringComparison.OrdinalIgnoreCase)).ToDictionary(d=>d.Id,StringComparer.OrdinalIgnoreCase);
        foreach(var old in pillSatellites.Keys.Where(id=>!wanted.ContainsKey(id)).ToArray()){pillSatellites[old].Close();pillSatellites.Remove(old);pillDisplayOverlap.Remove(old);}
        foreach(var (id,display) in wanted)
        {
            if(!pillSatellites.TryGetValue(id,out var satellite)){satellite=new(display,preferences.Value.PillPosition,preferences.Value.PillScale,preferences.Value.PillHoverDelayMs,ShouldExpandOnHover(preferences.Value.PillVisibilityMode),OpenPillOnDisplay);pillSatellites[id]=satellite;}
            satellite.Update(display,preferences.Value.PillPosition,preferences.Value.PillScale,preferences.Value.PillHoverDelayMs,ShouldExpandOnHover(preferences.Value.PillVisibilityMode),islandCurrentArtwork,islandMediaPlaying);
            var mode=preferences.Value.PillVisibilityMode;var visible=!pillObserverReady||mode!="peek";
            if(mode=="smart"&&pillObserverReady)
            {
                var bounds=PillDisplays.Edge(display,PillGeometry.CollapsedWidth*preferences.Value.PillScale,preferences.Value.PillPosition,(int)PillGeometry.CollapsedHeight);var overlap=PillDesktopObserver.ForegroundOverlaps(bounds);
                if(overlap.HasValue)pillDisplayOverlap[id]=overlap.Value;visible=!pillDisplayOverlap.GetValueOrDefault(id);
            }
            if(visible&&!satellite.IsVisible)satellite.Show();else if(!visible&&satellite.IsVisible)satellite.Hide();
        }
        RefreshPillAudioMeter();
    }
    internal void TriggerPillMediaPeek()
    {
        if(preferences.Value.PillVisibilityMode!="peek"||islandHiddenByAutomation||islandExpanded||!pillObserverReady||!DesktopGeometry.IsInteractive(this))return;
        pillMediaPeekDeadline=Environment.TickCount64+4000;RevealPillAutomatically(false);
        pillMediaPeekTimer??=new DispatcherTimer(DispatcherPriority.Background);pillMediaPeekTimer.Stop();pillMediaPeekTimer.Tick-=OnPillPeekElapsed;pillMediaPeekTimer.Tick+=OnPillPeekElapsed;pillMediaPeekTimer.Interval=TimeSpan.FromMilliseconds(4000);pillMediaPeekTimer.Start();
    }
    private void OnPillPeekElapsed(object? sender,EventArgs e)
    {
        pillMediaPeekTimer?.Stop();var remaining=pillMediaPeekDeadline-Environment.TickCount64;if(remaining>0){pillMediaPeekTimer!.Interval=TimeSpan.FromMilliseconds(remaining);pillMediaPeekTimer.Start();return;}RefreshPillVisibility();
    }
    private void OnPillDisplayChanged(object? sender,EventArgs e)
    {
        if(Dispatcher.HasShutdownStarted||Interlocked.Exchange(ref displayRefreshQueued,1)!=0)return;
        Dispatcher.BeginInvoke(()=>{Interlocked.Exchange(ref displayRefreshQueued,0);if(pillModeEnabled){PositionIsland();ConfigurePillVisibility();}},DispatcherPriority.Background);
    }
    private void StopPillVisibility(){StopPillAudioMeter();pillMediaPeekTimer?.Stop();pillForegroundTimer?.Stop();pillDesktopObserver?.Dispose();pillDesktopObserver=null;foreach(var window in pillSatellites.Values)window.Close();pillSatellites.Clear();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=OnPillDisplayChanged;}
}
