using System.Windows.Threading;
using Nashi.Core;

namespace Nashi.Windows;

// External editors often write a temporary file then rename it. Debounce notifications,
// validate off the UI thread, and never replace a more recent in-app edit.
internal sealed class PreferencesFileWatcher : IDisposable
{
    private readonly PreferencesStore store;
    private readonly Dispatcher dispatcher;
    private readonly Action changed;
    private readonly FileSystemWatcher watcher;
    private readonly DispatcherTimer debounce;
    private long generation;
    private bool disposed;

    internal PreferencesFileWatcher(PreferencesStore store,Dispatcher dispatcher,Action changed)
    {
        this.store=store;this.dispatcher=dispatcher;this.changed=changed;
        var directory=Path.GetDirectoryName(store.FilePath)!;Directory.CreateDirectory(directory);
        debounce=new DispatcherTimer(DispatcherPriority.Background,dispatcher){Interval=TimeSpan.FromMilliseconds(200)};
        debounce.Tick+=OnDebounced;
        watcher=new FileSystemWatcher(directory,Path.GetFileName(store.FilePath)){NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};
        watcher.Changed+=OnChanged;watcher.Created+=OnChanged;watcher.Renamed+=OnChanged;watcher.EnableRaisingEvents=true;
    }
    private void OnChanged(object sender,FileSystemEventArgs e)
    {
        if(disposed||dispatcher.HasShutdownStarted)return;
        dispatcher.BeginInvoke(()=>{if(disposed)return;generation++;debounce.Stop();debounce.Start();});
    }
    private async void OnDebounced(object? sender,EventArgs e)
    {
        debounce.Stop();var current=generation;var version=store.Revision;
        try
        {
            var snapshot=await Task.Run(store.ReadSnapshot);
            if(!disposed&&current==generation&&store.ApplySnapshot(snapshot,version))changed();
        }
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
        {
            // A partial or invalid external write must leave the last good settings intact.
        }
    }
    public void Dispose(){disposed=true;debounce.Stop();watcher.Dispose();}
}
