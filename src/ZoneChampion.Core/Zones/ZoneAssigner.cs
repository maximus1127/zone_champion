using ZoneChampion.Core.Geometry;

namespace ZoneChampion.Core.Zones;

/// <param name="Bounds">Visible window bounds (DWM extended frame bounds) in screen pixels.</param>
/// <param name="RestoredBounds">Where the window goes when restored from minimized/maximized.</param>
/// <param name="MonitorInstanceId">Instance id of the monitor the window is currently on.</param>
public readonly record struct WindowGeometry(
    RectI Bounds,
    RectI RestoredBounds,
    bool IsMinimized,
    bool IsMaximized,
    string? MonitorInstanceId);

/// <summary>Decides which zone a window belongs to: the zone holding the largest part of it.</summary>
public static class ZoneAssigner
{
    /// <param name="lastKnown">The zone the window was last assigned to, if any.</param>
    public static Zone? Assign(in WindowGeometry window, IReadOnlyList<Zone> zones, ZoneKey? lastKnown)
    {
        if (zones.Count == 0)
        {
            return null;
        }

        // A minimized window has no on-screen bounds; it stays with the zone it was minimized from.
        if (window.IsMinimized)
        {
            return Find(zones, lastKnown) ?? BestOverlap(window.RestoredBounds, zones, lastKnown);
        }

        // A maximized window covers every zone on its monitor, so the overlap test is meaningless.
        // Keep it in the zone it was in before maximizing.
        if (window.IsMaximized)
        {
            var monitorId = window.MonitorInstanceId;
            var monitorZones = zones
                .Where(z => string.Equals(z.Key.MonitorInstanceId, monitorId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (monitorZones.Count == 0)
            {
                return null;
            }

            return Find(monitorZones, lastKnown)
                ?? BestOverlap(window.RestoredBounds, monitorZones, lastKnown)
                ?? Nearest(window.RestoredBounds.Center, monitorZones);
        }

        return BestOverlap(window.Bounds, zones, lastKnown);
    }

    private static Zone? Find(IReadOnlyList<Zone> zones, ZoneKey? key)
    {
        if (key is null)
        {
            return null;
        }

        foreach (var zone in zones)
        {
            if (zone.Key == key.Value)
            {
                return zone;
            }
        }

        return null;
    }

    /// <summary>The zone with the largest overlap. Ties go to <paramref name="lastKnown"/> so a window split
    /// exactly between two zones doesn't flip back and forth.</summary>
    private static Zone? BestOverlap(RectI bounds, IReadOnlyList<Zone> zones, ZoneKey? lastKnown)
    {
        Zone? best = null;
        long bestArea = 0;
        foreach (var zone in zones)
        {
            long area = bounds.IntersectionArea(zone.Bounds);
            if (area > bestArea || (area == bestArea && area > 0 && zone.Key == lastKnown))
            {
                best = zone;
                bestArea = area;
            }
        }

        return best;
    }

    private static Zone Nearest(PointI point, IReadOnlyList<Zone> zones)
    {
        return zones.MinBy(z =>
        {
            long dx = z.Bounds.Center.X - point.X;
            long dy = z.Bounds.Center.Y - point.Y;
            return dx * dx + dy * dy;
        })!;
    }
}
