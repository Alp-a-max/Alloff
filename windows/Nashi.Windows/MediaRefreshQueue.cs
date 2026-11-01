namespace Nashi.Windows;

// Media sessions emit another track change while metadata/artwork is still being read.
// Coalesce bursts, but always read again after the in-flight request finishes.
internal sealed class MediaRefreshQueue
{
    private bool running,pending;
    internal async Task RequestAsync(Func<Task> refresh)
    {
        pending=true;if(running)return;running=true;
        try{do{pending=false;await refresh();}while(pending);}
        finally{running=false;}
    }
}
