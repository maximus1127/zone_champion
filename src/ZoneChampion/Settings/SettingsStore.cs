using System.IO;
using System.Threading;
using System.Windows.Threading;
using ZoneChampion.Core.Settings;

namespace ZoneChampion.Settings;

/// <summary>Loads %APPDATA%\ZoneChampion\settings.json and reloads it whenever it's saved.</summary>
internal sealed class SettingsStore : IDisposable
{
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _debounce;

    public string FilePath { get; } = Path.Combine(Log.FolderPath, "settings.json");

    /// <summary>The last settings that loaded successfully (defaults until then).</summary>
    public SettingsDocument Current { get; private set; } = SettingsDocument.Default;

    /// <summary>Why the file on disk isn't in effect, or null if it is.</summary>
    public string? LastError { get; private set; }

    public event Action? Changed;

    public event Action<string>? Failed;

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(Log.FolderPath);
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, DefaultSettings.Text);
                Log.Info($"Created default settings at {FilePath}");
            }

            Current = SettingsDocument.Parse(ReadWithRetry(FilePath));
            LastError = null;
            Log.Info("Settings loaded");
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is SettingsException or IOException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
            Log.Warn($"Settings not applied: {ex.Message}");
            Failed?.Invoke(ex.Message);
        }
    }

    public void Watch(Dispatcher dispatcher)
    {
        _debounce = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Normal, (_, _) =>
        {
            _debounce!.Stop();
            Load();
        }, dispatcher);
        _debounce.Stop();

        _watcher = new FileSystemWatcher(Log.FolderPath, Path.GetFileName(FilePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };

        // Editors save in different ways (write in place, or write a temp file and rename), so watch them all.
        FileSystemEventHandler onChange = (_, _) => dispatcher.BeginInvoke(Restart);
        _watcher.Changed += onChange;
        _watcher.Created += onChange;
        _watcher.Renamed += (_, _) => dispatcher.BeginInvoke(Restart);
        _watcher.EnableRaisingEvents = true;

        void Restart()
        {
            _debounce.Stop();
            _debounce.Start();
        }
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(50); // the editor may still hold the file
            }
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Stop();
    }
}
