using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Windows.Media.Control;

namespace Nashi.Windows;

public sealed partial class MainWindow
{
    private readonly MediaRefreshQueue islandMediaRefreshQueue=new();
    private DispatcherTimer? islandMediaRecoveryTimer;
    private DateTimeOffset islandMediaLastSeen;
    private int islandMediaReadFailures;
    private bool islandMediaStopped;
    private string? islandPreferredMediaSource;
    private System.Windows.Controls.ComboBox? islandMediaSourcePicker;
    private bool islandUpdatingMediaSources;
    private sealed record MediaSourceOption(string? Id,string Label){public override string ToString()=>Label;}
    private void UpdateMediaSourcePicker()
    {
        if(islandMediaSourcePicker is null||islandMediaManager is null)return;
        var sources=islandMediaManager.GetSessions().Select(session=>session.SourceAppUserModelId).Distinct().Order().ToArray();
        var existing=islandMediaSourcePicker.Items.Cast<MediaSourceOption>().Skip(1).Select(option=>option.Id).ToArray();
        if(!existing.SequenceEqual(sources))
        {
            islandUpdatingMediaSources=true;
            try
            {
                islandMediaSourcePicker.Items.Clear();islandMediaSourcePicker.Items.Add(new MediaSourceOption(null,IslandText("Automatic · active player","Otomatik · etkin oynatıcı")));
                foreach(var source in sources)islandMediaSourcePicker.Items.Add(new MediaSourceOption(source,Path.GetFileNameWithoutExtension(source)));
                islandMediaSourcePicker.SelectedItem=islandMediaSourcePicker.Items.Cast<MediaSourceOption>().FirstOrDefault(option=>option.Id==islandPreferredMediaSource)??islandMediaSourcePicker.Items[0];
            }
            finally{islandUpdatingMediaSources=false;}
        }
        islandMediaSourcePicker.Visibility=sources.Length>1?Visibility.Visible:Visibility.Collapsed;
    }

    private Task RefreshIslandMedia()=>App.IsSmokeTest||islandMediaStopped?Task.CompletedTask:islandMediaRefreshQueue.RequestAsync(ReadIslandMedia);

    private async Task ReadIslandMedia()
    {
        if(islandMediaStopped)return;
        try
        {
            islandMediaManager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if(islandMediaStopped)return;
            if(!islandMediaEventsConnected)
            {
                islandMediaManager.SessionsChanged+=OnIslandMediaSessionsChanged;
                islandMediaManager.CurrentSessionChanged+=OnIslandMediaCurrentSessionChanged;
                islandMediaEventsConnected=true;
            }
            UpdateMediaSourcePicker();var session=GetIslandMediaSession();ObserveIslandMediaSession(session);
            if(session is null)
            {
                // Browsers briefly remove/recreate their session between tracks.
                if(islandMediaLastSeen==default||DateTimeOffset.UtcNow-islandMediaLastSeen>TimeSpan.FromSeconds(3))ClearIslandMedia();
                return;
            }
            var info=await session.TryGetMediaPropertiesAsync();
            if(islandMediaStopped)return;
            if(string.IsNullOrWhiteSpace(info.Title)&&islandCurrentMedia is not null&&DateTimeOffset.UtcNow-islandMediaLastSeen<TimeSpan.FromSeconds(3))return;
            var playback=session.GetPlaybackInfo();
            var artworkKey=session.SourceAppUserModelId+"\0"+info.AlbumTitle+"\0"+info.Title+"\0"+info.Artist;
            var artwork=islandCurrentArtwork;
            if(artworkKey!=islandMediaArtworkKey)artwork=info.Thumbnail is null?null:await ReadIslandMediaArtwork(info.Thumbnail);
            if(islandMediaStopped)return;
            islandMediaLastSeen=DateTimeOffset.UtcNow;islandMediaReadFailures=0;
            islandMediaArtist=info.Artist;islandCurrentMedia=string.IsNullOrWhiteSpace(info.Title)?L10n.Text("Media is ready."):info.Title+(string.IsNullOrWhiteSpace(info.Artist)?"":" · "+info.Artist);
            islandMediaPlaying=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            islandMediaArtworkKey=artworkKey;islandCurrentArtwork=artwork;
            foreach(var image in new[]{islandMediaArtwork,islandNowArtwork,islandCompactPopupArtwork})if(image is not null)image.Source=artwork;
            if(islandMediaTitle is not null)islandMediaTitle.Text=string.IsNullOrWhiteSpace(info.Title)?L10n.Text("Media is ready."):info.Title;
            if(islandMediaArtistLabel is not null)islandMediaArtistLabel.Text=info.Artist;
            if(islandCompactMediaTitle is not null)islandCompactMediaTitle.Text=islandCurrentMedia;
            if(islandMediaEmpty is not null)islandMediaEmpty.Visibility=Visibility.Collapsed;
            foreach(var control in new[]{islandMediaPrevious,islandMediaNext,islandMediaPlaybackToggle,islandNowMediaToggle,islandCompactMediaToggle})if(control is not null)control.IsEnabled=true;
            try{UpdateIslandMediaTimeline(session);}catch(Exception){DisableIslandMediaSeeking();}
            foreach(var control in new[]{islandNowMediaToggle,islandMediaPlaybackToggle,islandCompactMediaToggle})if(control is not null){if(control.Content is TextBlock icon){var glyph=islandMediaPlaying?"\uE769":"\uE768";if(icon.Text!=glyph)icon.Text=glyph;}control.ToolTip=IslandText(islandMediaPlaying?"Pause media":"Play media",islandMediaPlaying?"Medyayı duraklat":"Medyayı sürdür");}
            RenderIslandMedia();UpdateIslandHint();
        }
        catch(Exception)
        {
            // A closed COM session is a recoverable source change, not an empty player.
            if(islandMediaStopped)return;
            if(++islandMediaReadFailures>=3){StopIslandMediaEvents();islandMediaManager=null;islandMediaReadFailures=0;}
            if(islandMediaLastSeen==default||DateTimeOffset.UtcNow-islandMediaLastSeen>TimeSpan.FromSeconds(5))ClearIslandMedia();
        }
    }
    private void DisableIslandMediaSeeking()
    {
        foreach(var seek in new[]{islandMediaSeek,islandCompactMediaSeek,islandNowSeek})if(seek is not null)seek.IsEnabled=false;
    }
    private void ClearIslandMedia()
    {
        islandCurrentMedia=null;islandMediaArtist=null;islandMediaArtworkKey=null;islandCurrentArtwork=null;islandMediaPlaying=false;
        foreach(var image in new[]{islandMediaArtwork,islandNowArtwork,islandCompactPopupArtwork})if(image is not null)image.Source=null;
        if(islandMediaTitle is not null)islandMediaTitle.Text=L10n.Text("No active media session.");
        if(islandMediaArtistLabel is not null)islandMediaArtistLabel.Text="";
        if(islandMediaEmpty is not null)islandMediaEmpty.Visibility=Visibility.Visible;
        if(islandCompactMediaTitle is not null)islandCompactMediaTitle.Text=L10n.Text("No active media session.");
        foreach(var control in new[]{islandMediaPrevious,islandMediaNext,islandMediaPlaybackToggle,islandNowMediaToggle,islandCompactMediaToggle})if(control is not null)control.IsEnabled=false;
        DisableIslandMediaSeeking();if(islandMediaPosition is not null)islandMediaPosition.Text="00:00";if(islandMediaDuration is not null)islandMediaDuration.Text="00:00";
        RenderIslandMedia();UpdateIslandHint();
    }
}
