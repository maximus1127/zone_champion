using System.Runtime.InteropServices;
using System.Windows.Threading;
using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Zones;
using ZoneChampion.Interop;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Tracking;

internal sealed class TrackedWindow
{
    public required nint Handle { get; init; }
    public required uint ProcessId { get; init; }

    /// <summary>Increases in the order windows were first seen; new windows go to the end of their panel.</summary>
    public required long Sequence { get; init; }

    public string? ExePath { get; init; }
    public string Title { get; set; } = "";
    public bool IsMinimized { get; set; }
    public bool IsMaximized { get; set; }
    public RectI Bounds { get; set; }
    public Zone? Zone { get; set; }
}

/// <summary>
/// Keeps the list of taskbar-style windows and the zone each one is in. Window events trigger a rescan, throttled
/// so dragging a window updates the panels live without rescanning on every pixel.
/// </summary>
internal sealed class WindowTracker : IDisposable
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    private readonly Dictionary<nint, TrackedWindow> _windows = new();
    private readonly DispatcherTimer _throttle;
    private readonly WinEventHooks _hooks;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private IReadOnlyList<Zone> _zones = [];
    private Dictionary<nint, DisplayMonitor> _monitors = new();
    private long _sequence;

    public WindowTracker()
    {
        _throttle = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(40) };
        _throttle.Tick += (_, _) =>
        {
            _throttle.Stop();
            Rescan();
        };
        _hooks = new WinEventHooks(OnWinEvent);
    }

    /// <summary>Raised after a rescan when windows appeared, disappeared, changed zone, title or state.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when a different window takes focus.</summary>
    public event EventHandler? ForegroundChanged;

    public nint Foreground { get; private set; }

    public IReadOnlyCollection<TrackedWindow> Windows => _windows.Values;

    public TrackedWindow? Find(nint hwnd) => _windows.GetValueOrDefault(hwnd);

    public void SetZones(IReadOnlyList<Zone> zones, IReadOnlyList<DisplayMonitor> monitors)
    {
        _zones = zones;
        _monitors = monitors.ToDictionary(m => m.Handle);
        Rescan(forceChanged: true);
    }

    public void RequestRescan()
    {
        if (!_throttle.IsEnabled)
        {
            _throttle.Start();
        }
    }

    private void OnWinEvent(uint eventType, nint hwnd)
    {
        switch (eventType)
        {
            case EVENT_SYSTEM_FOREGROUND:
                UpdateForeground();
                RequestRescan();
                break;
            case EVENT_OBJECT_LOCATIONCHANGE:
            case EVENT_OBJECT_NAMECHANGE:
                // Fired constantly for every window on screen; only ours matter.
                if (_windows.ContainsKey(hwnd))
                {
                    RequestRescan();
                }

                break;
            default:
                RequestRescan();
                break;
        }
    }

    public void Rescan(bool forceChanged = false)
    {
        var found = new List<nint>();
        EnumWindows((hwnd, _) =>
        {
            if (IsTaskbarWindow(hwnd))
            {
                found.Add(hwnd);
            }

            return true;
        }, 0);

        // EnumWindows lists front-to-back; number never-seen windows back-to-front so the startup order is stable.
        found.Reverse();

        bool changed = forceChanged;
        var seen = new HashSet<nint>(found.Count);
        foreach (var hwnd in found)
        {
            GetWindowThreadProcessId(hwnd, out uint processId);
            if (!_windows.TryGetValue(hwnd, out var window) || window.ProcessId != processId)
            {
                // New window, or a closed window's handle reused by another process.
                window = new TrackedWindow
                {
                    Handle = hwnd,
                    ProcessId = processId,
                    Sequence = _sequence++,
                    ExePath = GetProcessPath(processId),
                };
                _windows[hwnd] = window;
                changed = true;
            }

            seen.Add(hwnd);
            changed |= Update(window);
        }

        foreach (var hwnd in _windows.Keys.Where(h => !seen.Contains(h)).ToList())
        {
            _windows.Remove(hwnd);
            changed = true;
        }

        UpdateForeground();
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Update(TrackedWindow window)
    {
        var hwnd = window.Handle;
        string title = GetTitle(hwnd);
        bool minimized = IsIconic(hwnd);
        bool maximized = !minimized && IsZoomed(hwnd);
        var bounds = GetVisibleBounds(hwnd);

        var monitorHandle = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var monitorId = _monitors.TryGetValue(monitorHandle, out var monitor) ? monitor.InstanceId : null;
        var geometry = new WindowGeometry(bounds, GetRestoredBounds(hwnd), minimized, maximized, monitorId);
        var zone = ZoneAssigner.Assign(geometry, _zones, window.Zone?.Key);

        bool changed = title != window.Title
            || minimized != window.IsMinimized
            || maximized != window.IsMaximized
            || zone?.Key != window.Zone?.Key
            || zone?.Bounds != window.Zone?.Bounds;

        window.Title = title;
        window.IsMinimized = minimized;
        window.IsMaximized = maximized;
        window.Bounds = bounds;
        window.Zone = zone;
        return changed;
    }

    private void UpdateForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground != Foreground)
        {
            Foreground = foreground;
            ForegroundChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Approximates the taskbar's rules for which windows get a button.</summary>
    private bool IsTaskbarWindow(nint hwnd)
    {
        if (!IsWindowVisible(hwnd))
        {
            return false;
        }

        long exStyle = GetExStyle(hwnd);
        if ((exStyle & WS_EX_APPWINDOW) == 0)
        {
            if ((exStyle & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE)) != 0 || GetWindow(hwnd, GW_OWNER) != 0)
            {
                return false;
            }
        }

        // Cloaked = on another virtual desktop, or a suspended/hidden UWP frame.
        if (IsCloaked(hwnd))
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == _ownProcessId)
        {
            return false;
        }

        return !ShellClasses.Contains(GetClassName(hwnd));
    }

    /// <summary>
    /// Where the window returns to when un-minimized or un-maximized. GetWindowPlacement reports this in workspace
    /// coordinates (relative to the work area), so shift it back into screen coordinates.
    /// </summary>
    private RectI GetRestoredBounds(nint hwnd)
    {
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref placement))
        {
            return default;
        }

        var rect = placement.rcNormalPosition;
        var monitorHandle = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
        if (_monitors.TryGetValue(monitorHandle, out var monitor))
        {
            return rect.ToRectI().Offset(monitor.WorkArea.Left - monitor.Bounds.Left, monitor.WorkArea.Top - monitor.Bounds.Top);
        }

        return rect.ToRectI();
    }

    public void Dispose()
    {
        _throttle.Stop();
        _hooks.Dispose();
    }
}
