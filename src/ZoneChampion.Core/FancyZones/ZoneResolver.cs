using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.FancyZones;

/// <summary>Turns FancyZones' settings plus the connected monitors into screen-space zones.</summary>
public static class ZoneResolver
{
    public static IReadOnlyList<Zone> Resolve(
        FancyZonesData data,
        IReadOnlyList<DisplayMonitor> monitors,
        Guid currentVirtualDesktop,
        Guid lastUsedVirtualDesktop)
    {
        var zones = new List<Zone>();
        foreach (var monitor in monitors)
        {
            var layout = FindLayout(data, monitor, currentVirtualDesktop, lastUsedVirtualDesktop);
            var custom = layout.Type.Equals("custom", StringComparison.OrdinalIgnoreCase) ? data.FindCustomLayout(layout.Uuid) : null;
            var work = monitor.WorkArea;
            foreach (var (index, rect) in LayoutCalculator.Calculate(layout, custom, work.Width, work.Height, monitor.Dpi))
            {
                zones.Add(new Zone(new ZoneKey(monitor.InstanceId, index), rect.Offset(work.Left, work.Top), monitor));
            }
        }

        return zones;
    }

    /// <summary>
    /// Picks the layout FancyZones would use for a monitor on the current virtual desktop. FancyZones rewrites
    /// applied-layouts.json when it has to fall back, so the fallbacks here only matter briefly.
    /// </summary>
    public static FzLayout FindLayout(FancyZonesData data, DisplayMonitor monitor, Guid currentVirtualDesktop, Guid lastUsedVirtualDesktop)
    {
        var applied = Match(currentVirtualDesktop)
            ?? (lastUsedVirtualDesktop != currentVirtualDesktop ? Match(lastUsedVirtualDesktop) : null);
        if (applied is not null)
        {
            return applied.Layout;
        }

        bool vertical = monitor.Bounds.Height > monitor.Bounds.Width;
        var fallback = data.DefaultLayouts.LastOrDefault(d =>
            d.MonitorConfiguration.Equals("vertical", StringComparison.OrdinalIgnoreCase) == vertical);
        if (fallback is not null)
        {
            return fallback.Layout;
        }

        return new FzLayout(vertical ? "rows" : "priority-grid", Guid.Empty, ShowSpacing: true, Spacing: 16, ZoneCount: 3);

        FzAppliedLayout? Match(Guid virtualDesktop) =>
            data.AppliedLayouts.FirstOrDefault(a => a.Device.VirtualDesktop == virtualDesktop && DeviceMatches(a.Device, monitor));
    }

    private static bool DeviceMatches(FzDevice device, DisplayMonitor monitor) =>
        string.Equals(device.MonitorId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase)
        && (string.Equals(device.MonitorInstance, monitor.InstanceId, StringComparison.OrdinalIgnoreCase)
            || device.MonitorNumber == monitor.MonitorNumber);
}
