using System.Windows.Media;
using System.Windows.Threading;
using ZoneChampion.Core.Panels;
using ZoneChampion.Core.Settings;
using ZoneChampion.Core.Zones;
using ZoneChampion.Interop;
using ZoneChampion.Tracking;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Panels;

/// <summary>Owns one <see cref="PanelWindow"/> per zone and keeps each in sync with the windows in that zone.</summary>
internal sealed class PanelManager : IPanelHost, IDisposable
{
    private static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal) { "Progman", "WorkerW" };

    private readonly WindowTracker _tracker;
    private readonly IconLoader _icons;
    private readonly Func<SettingsDocument> _settings;
    private readonly Dispatcher _dispatcher;
    private readonly PanelOrdering _ordering = new();
    private readonly Dictionary<ZoneKey, PanelWindow> _panels = new();
    private readonly Dictionary<ZoneKey, PanelTheme> _themes = new();
    private readonly Dictionary<nint, string> _tags = new();
    private const int MaxIconAttempts = 3;
    private static readonly TimeSpan IconRecheckDelay = TimeSpan.FromSeconds(5);

    private readonly Dictionary<(nint Handle, int Size), IconEntry> _iconEntries = new();
    private IReadOnlyList<Zone> _zones = [];
    private SettingsDocument? _themesBuiltFrom;
    private bool _panelsHidden;

    public PanelManager(WindowTracker tracker, IconLoader icons, Func<SettingsDocument> settings, Dispatcher dispatcher)
    {
        _tracker = tracker;
        _icons = icons;
        _settings = settings;
        _dispatcher = dispatcher;
    }

    public IEnumerable<PanelWindow> Panels => _panels.Values;

    public IReadOnlyList<Zone> Zones => _zones;

    public bool PanelsHidden
    {
        get => _panelsHidden;
        set
        {
            _panelsHidden = value;
            Refresh();
        }
    }

    public void SetZones(IReadOnlyList<Zone> zones) => _zones = zones;

    public void Refresh()
    {
        var settings = _settings();
        if (!ReferenceEquals(settings, _themesBuiltFrom))
        {
            _themes.Clear();
            _themesBuiltFrom = settings;
        }

        var assignments = _tracker.Windows
            .Where(w => w.Zone is not null)
            .OrderBy(w => w.Sequence)
            .Select(w => (w.Handle, w.Zone!.Key))
            .ToList();
        _ordering.Apply(assignments, IsWindow);
        ForgetClosedWindows();

        var fullscreenMonitor = FindFullscreenMonitor();
        var live = new HashSet<ZoneKey>();

        foreach (var zone in _zones)
        {
            var resolved = settings.Resolve(zone.Monitor.DeviceId, zone.Monitor.InstanceId, zone.Key.Number);
            if (!resolved.Enabled)
            {
                continue;
            }

            var style = resolved.Style;
            var handles = _ordering.Get(zone.Key);
            bool show = !_panelsHidden
                && (handles.Count > 0 || !style.HideWhenEmpty)
                && !(style.HideOnFullscreen && fullscreenMonitor == zone.Monitor.InstanceId);

            var theme = GetTheme(zone, style);
            if (!_panels.TryGetValue(zone.Key, out var panel))
            {
                if (!show)
                {
                    continue;
                }

                panel = new PanelWindow(new PanelViewModel(zone.Key, theme), this);
                _panels[zone.Key] = panel;
            }

            live.Add(zone.Key);
            panel.Configure(zone, theme);

            var states = new List<PanelItemState>(handles.Count);
            foreach (var handle in handles)
            {
                if (_tracker.Find(handle) is { } window)
                {
                    states.Add(new PanelItemState(handle, window.Title, handle == _tracker.Foreground, window.IsMinimized, _tags.GetValueOrDefault(handle)));
                }
            }

            panel.ViewModel.Sync(states);

            int iconPixels = (int)Math.Round(theme.IconSize * zone.Monitor.Scale);
            foreach (var item in panel.ViewModel.Items)
            {
                UpdateIcon(item, iconPixels);
            }

            panel.SetShown(show);
        }

        foreach (var key in _panels.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _panels[key].Close();
            _panels.Remove(key);
        }
    }

    /// <summary>Another window took focus: stay above it and dismiss any open menu.</summary>
    public void OnForegroundChanged()
    {
        foreach (var panel in _panels.Values)
        {
            panel.CloseMenu();
            panel.ReassertTopmost();
        }
    }

    private PanelTheme GetTheme(Zone zone, PanelStyle style)
    {
        var edge = PanelPlacement.ResolveEdge(style.Edge, zone.Bounds, zone.Monitor.WorkArea);
        if (_themes.TryGetValue(zone.Key, out var theme) && ReferenceEquals(theme.Style, style) && theme.Edge == edge)
        {
            return theme;
        }

        theme = new PanelTheme(style, edge);
        _themes[zone.Key] = theme;
        return theme;
    }

    /// <summary>
    /// Shows the cached icon for an item and loads it when needed. Every icon is checked once more a few seconds
    /// after the first load, since some apps set their window icon only after the window appears; icons that
    /// couldn't be found at all are retried a couple of times.
    /// </summary>
    private void UpdateIcon(PanelItemViewModel item, int size)
    {
        var key = (item.Handle, size);
        if (!_iconEntries.TryGetValue(key, out var entry))
        {
            entry = new IconEntry();
            _iconEntries[key] = entry;
        }

        if (item.IconPixels != size)
        {
            item.IconPixels = size;
            item.Icon = entry.Image;
        }

        bool due = entry.Attempts == 0
            || (entry.Attempts < MaxIconAttempts
                && (entry.Image is null || entry.Attempts == 1)
                && DateTime.UtcNow - entry.LoadedAt > IconRecheckDelay);
        if (entry.Pending || !due || _tracker.Find(item.Handle) is not { } window)
        {
            return;
        }

        entry.Pending = true;
        _icons.Load(window, size, image =>
        {
            entry.Pending = false;
            entry.Attempts++;
            entry.LoadedAt = DateTime.UtcNow;
            entry.Image = image ?? entry.Image; // never replace a good icon with nothing

            foreach (var panel in _panels.Values)
            {
                if (panel.ViewModel.Find(key.Handle) is { } target && target.IconPixels == size)
                {
                    target.Icon = entry.Image;
                }
            }
        });
    }

    private void ForgetClosedWindows()
    {
        foreach (var handle in _tags.Keys.Where(h => _tracker.Find(h) is null && !IsWindow(h)).ToList())
        {
            _tags.Remove(handle);
        }

        foreach (var key in _iconEntries.Keys.Where(k => _tracker.Find(k.Handle) is null).ToList())
        {
            _iconEntries.Remove(key);
        }
    }

    private sealed class IconEntry
    {
        public ImageSource? Image { get; set; }
        public DateTime LoadedAt { get; set; }
        public int Attempts { get; set; }
        public bool Pending { get; set; }
    }

    /// <summary>The monitor (instance id) covered by a full-screen foreground app, if any.</summary>
    private string? FindFullscreenMonitor()
    {
        var foreground = _tracker.Foreground;
        if (foreground == 0 || !IsWindow(foreground) || IsZoomed(foreground) || IsIconic(foreground))
        {
            return null;
        }

        GetWindowThreadProcessId(foreground, out uint processId);
        if (processId == Environment.ProcessId || DesktopClasses.Contains(GetClassName(foreground)) || !GetWindowRect(foreground, out var rect))
        {
            return null;
        }

        // Full-screen apps (videos, games, slideshows, F11 browsers) drop their title bar. An ordinary window that
        // happens to be as big as the monitor keeps it, and shouldn't hide the panels.
        if ((GetWindowLongPtr(foreground, GWL_STYLE) & WS_CAPTION) == WS_CAPTION)
        {
            return null;
        }

        var monitorHandle = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        var monitor = _zones.Select(z => z.Monitor).FirstOrDefault(m => m.Handle == monitorHandle);
        return monitor is not null && rect.ToRectI().Contains(monitor.Bounds) ? monitor.InstanceId : null;
    }

    // ---- IPanelHost ----

    public void Activate(nint handle) => WindowActions.ToggleFocus(handle);

    public void Reorder(ZoneKey zone, nint handle, int newIndex) => _ordering.Move(zone, handle, newIndex);

    public void SetTag(nint handle, string? color)
    {
        if (color is null)
        {
            _tags.Remove(handle);
        }
        else
        {
            _tags[handle] = color;
        }

        Refresh();
    }

    public void CloseWindow(nint handle) => WindowActions.Close(handle);

    public IReadOnlyList<(ZoneKey Key, string Label)> GetMoveTargets(ZoneKey from)
    {
        bool multipleMonitors = _zones.Select(z => z.Key.MonitorInstanceId).Distinct().Count() > 1;
        return _zones
            .Where(z => z.Key != from)
            .OrderBy(z => z.Key.MonitorInstanceId != from.MonitorInstanceId)
            .ThenBy(z => z.Monitor.Bounds.Left)
            .ThenBy(z => z.Key.Index)
            .Select(z => (z.Key, multipleMonitors && z.Key.MonitorInstanceId != from.MonitorInstanceId
                ? $"Zone {z.Key.Number}  ·  {z.Monitor.DeviceId}"
                : $"Zone {z.Key.Number}"))
            .ToList();
    }

    public void MoveToZone(nint handle, ZoneKey zone)
    {
        if (_zones.FirstOrDefault(z => z.Key == zone) is { } target)
        {
            WindowActions.MoveTo(handle, target.Bounds, _dispatcher);
        }
    }

    public void Dispose()
    {
        foreach (var panel in _panels.Values)
        {
            panel.Close();
        }

        _panels.Clear();
    }
}
