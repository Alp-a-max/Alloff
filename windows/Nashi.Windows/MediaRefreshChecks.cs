namespace Nashi.Windows;

internal static class MediaRefreshChecks
{
    internal static void Run(Action<string,bool,string?> check)
    {
        var queue=new MediaRefreshQueue();var release=new TaskCompletionSource();var title="first track";var displayed="";var reads=0;var active=0;var maximumActive=0;
        async Task Read()
        {
            active++;maximumActive=Math.Max(active,maximumActive);reads++;var snapshot=title;
            if(reads==1)await release.Task;
            displayed=snapshot;active--;
        }
        var first=queue.RequestAsync(Read);title="next track";
        queue.RequestAsync(Read).GetAwaiter().GetResult();queue.RequestAsync(Read).GetAwaiter().GetResult();release.SetResult();first.GetAwaiter().GetResult();
        check("media-track-change-during-metadata-read",reads==2&&displayed=="next track"&&maximumActive==1,$"reads={reads}, displayed={displayed}, simultaneous={maximumActive}");
        try{queue.RequestAsync(()=>Task.FromException(new InvalidOperationException("closed session"))).GetAwaiter().GetResult();}catch(InvalidOperationException){}
        title="replacement session";queue.RequestAsync(Read).GetAwaiter().GetResult();
        check("media-refresh-recovers-after-closed-session",displayed=="replacement session",displayed);
    }
}
