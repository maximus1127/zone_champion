using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using ZoneChampion.Core.FancyZones;
using ZoneChampion.Core.Zones;
using ZoneChampion.Interop;
using ZoneChampion.Panels;
using ZoneChampion.Settings;
using ZoneChampion.Tracking;

namespace ZoneChampion;

/// <summary>Wires settings, FancyZones data, window tracking, panels and the tray icon together.</summary>
internal sealed class AppController : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly SettingsStore _settings = new();
    private readonly List<Window> _overlays = new();
    private IconLoader? _icons;
    private WindowTracker? _tracker;
    private PanelManager? _panels;
    private TrayIcon? _tray;
    private FileSystemWatcher? _fancyZonesWatcher;
    private DispatcherTimer? _zoneReload;
    private DispatcherTimer? _heartbeat;
    private string _monitorSignature = "";
    private Guid _virtualDesktop;
    private string? _fancyZonesFolder;
    private bool _warnedNoFancyZones;

    public string FancyZonesFolder => _settings.Current.FancyZonesDataFolder is { Length: > 0 } folder
        ? Environment.ExpandEnvironmentVariables(folder)
        : FancyZonesData.DefaultFolder;

    public PanelManager Panels => _panels ?? throw new InvalidOperationException("Not started.");

    public void Start()
    {
        Log.Info($"Zone Champion {typeof(AppController).Assembly.GetName().Version} starting");

        _settings.Load();
        AutoStart.Apply(_settings.Current.StartWithWindows);

        _icons = new IconLoader(_dispatcher);
        _tracker = new WindowTracker();
        _panels = new PanelManager(_tracker, _icons, () => _settings.Current, _dispatcher);
        _tracker.Changed += (_, _) => _panels.Refresh();
        _tracker.ForegroundChanged += (_, _) =>
        {
            _panels.OnForegroundChanged();
            _panels.Refresh();
        };

        _tray = new TrayIcon();
        _tray.IdentifyZones += IdentifyZones;
        _tray.PanelsHiddenChanged += hidden => _panels.PanelsHidden = hidden;
        _tray.EditSettings += () => OpenInShell(_settings.FilePath, edit: true);
        _tray.OpenSettingsFolder += () => OpenInShell(Log.FolderPath, edit: false);
        _tray.Reload += () =>
        {
            _settings.Load();
            ReloadZones();
        };
        _tray.Exit += () => Application.Current.Shutdown();

        _settings.Changed += OnSettingsChanged;
        _settings.Failed += message => _tray.ShowWarning("Settings not applied", message);
        _settings.Watch(_dispatcher);
        if (_settings.LastError is { } error)
        {
            _tray.ShowWarning("Settings not applied", error);
        }

        _zoneReload = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Normal, (_, _) =>
        {
            _zoneReload!.Stop();
            ReloadZones();
        }, _dispatcher);
        _zoneReload.Stop();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // Safety net for anything events miss: work-area changes, virtual desktop switches, elevated windows.
        _heartbeat = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Heartbeat(), _dispatcher);

        ReloadZones();
    }

    private void OnSettingsChanged()
    {
        AutoStart.Apply(_settings.Current.StartWithWindows);
        if (!string.Equals(_fancyZonesFolder, FancyZonesFolder, StringComparison.OrdinalIgnoreCase))
        {
            ReloadZones();
        }
        else
        {
            _panels?.Refresh();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => _dispatcher.BeginInvoke(ScheduleZoneReload);

    private void ScheduleZoneReload()
    {
        _zoneReload?.Stop();
        _zoneReload?.Start();
    }

    public void ReloadZones()
    {
        if (_tracker is null || _panels is null)
        {
            return;
        }

        var folder = FancyZonesFolder;
        WatchFancyZones(folder);

        var monitors = Monitors.Enumerate();
        _monitorSignature = Monitors.Signature(monitors);
        _virtualDesktop = VirtualDesktops.Current();

        var data = FancyZonesData.Load(folder);
        foreach (var warning in data.Warnings)
        {
            Log.Warn($"FancyZones data: {warning}");
        }

        IReadOnlyList<Zone> zones = [];
        if (data.FolderFound)
        {
            zones = ZoneResolver.Resolve(data, monitors, _virtualDesktop, VirtualDesktops.LastUsed(folder));
        }
        else if (!_warnedNoFancyZones)
        {
            _warnedNoFancyZones = true;
            Log.Warn($"FancyZones data folder not found: {folder}");
            _tray?.ShowWarning("FancyZones not found", $"No FancyZones layouts in {folder}. Is PowerToys installed?");
        }

        Log.Info($"{zones.Count} zones on {monitors.Count} monitors: " + string.Join("; ", zones.Select(z => $"{z.Monitor.DeviceId} zone {z.Key.Number} {z.Bounds}")));

        _panels.SetZones(zones);
        _tracker.SetZones(zones, monitors); // rescans and raises Changed, which refreshes the panels
    }

    private void Heartbeat()
    {
        if (_tracker is null || _panels is null)
        {
            return;
        }

        // Also catches FancyZones' folder appearing after startup (PowerToys installed or first run later).
        bool fancyZonesAppeared = _fancyZonesWatcher is null && Directory.Exists(FancyZonesFolder);
        if (fancyZonesAppeared || Monitors.Signature(Monitors.Enumerate()) != _monitorSignature || VirtualDesktops.Current() != _virtualDesktop)
        {
            ReloadZones();
            return;
        }

        _tracker.RequestRescan();
        _panels.Refresh();
    }

    private void WatchFancyZones(string folder)
    {
        if (_fancyZonesWatcher is not null && string.Equals(_fancyZonesFolder, folder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _fancyZonesWatcher?.Dispose();
        _fancyZonesWatcher = null;
        _fancyZonesFolder = folder;
        if (!Directory.Exists(folder))
        {
            return; // the heartbeat retries once the folder exists
        }

        _fancyZonesWatcher = new FileSystemWatcher(folder, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        FileSystemEventHandler onChange = (_, e) =>
        {
            if (e.Name is FancyZonesData.AppliedLayoutsFile or FancyZonesData.CustomLayoutsFile or FancyZonesData.DefaultLayoutsFile)
            {
                _dispatcher.BeginInvoke(ScheduleZoneReload);
            }
        };
        _fancyZonesWatcher.Changed += onChange;
        _fancyZonesWatcher.Created += onChange;
        _fancyZonesWatcher.Renamed += (_, e) => onChange(null!, e);
        _fancyZonesWatcher.Error += (_, e) => _dispatcher.BeginInvoke(() =>
        {
            // The folder was deleted or the watcher overflowed; drop it so the heartbeat recreates it.
            Log.Warn($"FancyZones folder watcher failed: {e.GetException().Message}");
            _fancyZonesWatcher?.Dispose();
            _fancyZonesWatcher = null;
        });
        _fancyZonesWatcher.EnableRaisingEvents = true;
    }

    private void IdentifyZones()
    {
        // Closing an overlay removes it from the list, so iterate a copy.
        foreach (var overlay in _overlays.ToList())
        {
            overlay.Close();
        }

        _overlays.Clear();
        if (_panels is null)
        {
            return;
        }

        if (_panels.Zones.Count == 0)
        {
            _tray?.ShowWarning("No zones", "No FancyZones layouts were found for the connected monitors.");
            return;
        }

        foreach (var zone in _panels.Zones)
        {
            bool enabled = _settings.Current.Resolve(zone.Monitor.DeviceId, zone.Monitor.InstanceId, zone.Key.Number).Enabled;
            var overlay = new IdentifyOverlay(zone, enabled, TimeSpan.FromSeconds(6));
            overlay.Closed += (_, _) => _overlays.Remove(overlay);
            _overlays.Add(overlay);
            overlay.Show();
        }
    }

    private static void OpenInShell(string path, bool edit)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (edit && ex is System.ComponentModel.Win32Exception)
        {
            // No app associated with .json files.
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _heartbeat?.Stop();
        _zoneReload?.Stop();
        _fancyZonesWatcher?.Dispose();
        _settings.Dispose();
        foreach (var overlay in _overlays.ToList())
        {
            overlay.Close();
        }

        _panels?.Dispose();
        _tracker?.Dispose();
        _icons?.Dispose();
        _tray?.Dispose();
        Log.Info("Zone Champion stopped");
    }
}
