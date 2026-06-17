using System.IO;
using System.Windows.Threading;

namespace MDViewer.Services;

public sealed class FileWatcherService : IDisposable
{
    private readonly DispatcherTimer _debounceTimer;
    private FileSystemWatcher? _watcher;
    private string? _currentPath;

    public FileWatcherService()
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            if (_currentPath is not null)
            {
                FileChanged?.Invoke(this, _currentPath);
            }
        };
    }

    public event EventHandler<string>? FileChanged;

    public void Watch(string path)
    {
        Stop();

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        _currentPath = path;
        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer.Stop();
    }

    public void Dispose() => Stop();

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }
}
