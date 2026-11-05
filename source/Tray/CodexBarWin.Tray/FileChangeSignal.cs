using System;
using System.IO;
using System.Threading;

namespace CodexBarWin.Tray;

/// <summary>
/// Calls <c>changed</c> once, on a thread-pool thread, shortly after a burst of changes to one file.
/// TokenTray uses it to show live usage as soon as Claude Code writes a renewed login.
/// </summary>
internal sealed class FileChangeSignal : IDisposable
{
    private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounce;
    private readonly TimeSpan _delay;
    private int _disposed;

    private FileChangeSignal(string directory, string fileName, Action changed, TimeSpan delay)
    {
        _delay = delay;
        _debounce = new Timer(_ => { if (Volatile.Read(ref _disposed) == 0) changed(); });
        _watcher = new FileSystemWatcher(directory, fileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };
        _watcher.Changed += OnChange;
        _watcher.Created += OnChange;
        // Atomic writers replace the file by renaming a temporary file onto it.
        _watcher.Renamed += OnChange;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Returns null when the folder does not exist or cannot be watched; the regular refresh still runs.</summary>
    public static FileChangeSignal? TryCreate(string directory, string fileName, Action changed, TimeSpan? delay = null)
    {
        try
        {
            return Directory.Exists(directory) ? new FileChangeSignal(directory, fileName, changed, delay ?? DefaultDelay) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private void OnChange(object sender, FileSystemEventArgs e)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        try { _debounce.Change(_delay, Timeout.InfiniteTimeSpan); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
