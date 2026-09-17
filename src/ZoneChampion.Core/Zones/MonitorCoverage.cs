namespace ZoneChampion.Core.Zones;

/// <summary>A window's place in the stacking order, as far as monitor coverage is concerned.</summary>
public readonly record struct StackedWindow(string? MonitorInstanceId, bool IsMinimized, bool IsMaximized);

public static class MonitorCoverage
{
    /// <summary>
    /// Monitors whose frontmost (non-minimized) window is maximized. On those monitors the zones are hidden behind
    /// one window, so their panels have nothing useful to show.
    /// </summary>
    /// <param name="frontToBack">Taskbar windows in stacking order, frontmost first.</param>
    public static IReadOnlySet<string> CoveredByMaximizedWindow(IEnumerable<StackedWindow> frontToBack)
    {
        var decided = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in frontToBack)
        {
            if (window.IsMinimized || window.MonitorInstanceId is not { } monitor || !decided.Add(monitor))
            {
                continue;
            }

            if (window.IsMaximized)
            {
                covered.Add(monitor);
            }
        }

        return covered;
    }
}
