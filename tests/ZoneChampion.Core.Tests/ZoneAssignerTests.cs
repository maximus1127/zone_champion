using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Tests;

public class ZoneAssignerTests
{
    private static readonly DisplayMonitor MonitorA = new(1, "A", "A", "A", 1, new RectI(0, 0, 2000, 1000), new RectI(0, 0, 2000, 1000), 96);
    private static readonly DisplayMonitor MonitorB = new(2, "B", "B", "B", 2, new RectI(2000, 0, 3000, 1000), new RectI(2000, 0, 3000, 1000), 96);

    private static readonly Zone Left = new(new ZoneKey("A", 0), new RectI(0, 0, 1000, 1000), MonitorA);
    private static readonly Zone Right = new(new ZoneKey("A", 1), new RectI(1000, 0, 2000, 1000), MonitorA);
    private static readonly Zone Other = new(new ZoneKey("B", 0), new RectI(2000, 0, 3000, 1000), MonitorB);
    private static readonly Zone[] Zones = [Left, Right, Other];

    private static WindowGeometry Normal(RectI bounds) => new(bounds, bounds, false, false, "A");

    [Fact]
    public void PicksZoneHoldingMostOfTheWindow()
    {
        var window = Normal(new RectI(600, 100, 1500, 900)); // 400 px in Left, 500 px in Right

        Assert.Equal(Right, ZoneAssigner.Assign(window, Zones, null));
    }

    [Fact]
    public void ExactTie_KeepsLastKnownZone()
    {
        var window = Normal(new RectI(500, 100, 1500, 900));

        Assert.Equal(Right, ZoneAssigner.Assign(window, Zones, Right.Key));
        Assert.Equal(Left, ZoneAssigner.Assign(window, Zones, Left.Key));
    }

    [Fact]
    public void WindowOutsideEveryZone_IsUnassigned()
    {
        Assert.Null(ZoneAssigner.Assign(Normal(new RectI(-900, 0, -100, 500)), Zones, Left.Key));
    }

    [Fact]
    public void Minimized_StaysInLastKnownZone()
    {
        var offscreen = new RectI(-32000, -32000, -31840, -31972);
        var window = new WindowGeometry(offscreen, new RectI(100, 100, 400, 400), IsMinimized: true, IsMaximized: false, "A");

        Assert.Equal(Right, ZoneAssigner.Assign(window, Zones, Right.Key));
        Assert.Equal(Left, ZoneAssigner.Assign(window, Zones, null));
    }

    [Fact]
    public void Maximized_UsesLastKnownZoneOnSameMonitor()
    {
        var window = new WindowGeometry(new RectI(-8, -8, 2008, 1008), new RectI(100, 100, 400, 400), false, IsMaximized: true, "A");

        Assert.Equal(Right, ZoneAssigner.Assign(window, Zones, Right.Key));
    }

    [Fact]
    public void Maximized_OnDifferentMonitor_UsesThatMonitorsZones()
    {
        // Moved to monitor B while maximized; the restored rect is still over monitor A.
        var window = new WindowGeometry(new RectI(1992, -8, 3008, 1008), new RectI(100, 100, 400, 400), false, IsMaximized: true, "B");

        Assert.Equal(Other, ZoneAssigner.Assign(window, Zones, Left.Key));
    }
}
