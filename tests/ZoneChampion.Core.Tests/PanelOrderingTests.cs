using ZoneChampion.Core.Panels;
using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Tests;

public class PanelOrderingTests
{
    private static readonly ZoneKey Zone1 = new("M", 0);
    private static readonly ZoneKey Zone2 = new("M", 1);

    private static bool Dead(nint _) => false;

    [Fact]
    public void NewWindowsAppendInDiscoveryOrder()
    {
        var ordering = new PanelOrdering();

        ordering.Apply([(1, Zone1), (2, Zone1)], Dead);
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1)], Dead);

        Assert.Equal([1, 2, 3], ordering.Get(Zone1));
    }

    [Fact]
    public void MovedOrderSurvivesUpdates()
    {
        var ordering = new PanelOrdering();
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1)], Dead);

        Assert.True(ordering.Move(Zone1, 3, 0));
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1), (4, Zone1)], Dead);

        Assert.Equal([3, 1, 2, 4], ordering.Get(Zone1));
    }

    [Fact]
    public void MoveToEnd()
    {
        var ordering = new PanelOrdering();
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1)], Dead);

        ordering.Move(Zone1, 1, 2);

        Assert.Equal([2, 3, 1], ordering.Get(Zone1));
    }

    [Fact]
    public void WindowMovingToAnotherZone_LeavesOldPanelAndAppendsToNew()
    {
        var ordering = new PanelOrdering();
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone2)], Dead);

        ordering.Apply([(2, Zone1), (3, Zone2), (1, Zone2)], Dead);

        Assert.Equal([2], ordering.Get(Zone1));
        Assert.Equal([3, 1], ordering.Get(Zone2));
    }

    [Fact]
    public void ClosedWindowsAreForgotten()
    {
        var ordering = new PanelOrdering();
        ordering.Apply([(1, Zone1), (2, Zone1)], Dead);

        ordering.Apply([(2, Zone1)], Dead);
        ordering.Apply([(2, Zone1), (1, Zone1)], Dead); // handle reused by a new window

        Assert.Equal([2, 1], ordering.Get(Zone1));
    }

    [Fact]
    public void HiddenButAliveWindow_KeepsItsSlot()
    {
        var ordering = new PanelOrdering();
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1)], Dead);
        ordering.Move(Zone1, 3, 0); // [3, 1, 2]

        ordering.Apply([(1, Zone1), (2, Zone1)], w => w == 3); // 3 is on another virtual desktop
        Assert.Equal([1, 2], ordering.Get(Zone1));

        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1)], Dead);
        Assert.Equal([3, 1, 2], ordering.Get(Zone1));
    }

    [Fact]
    public void MoveWithHiddenWindowsBetween()
    {
        var ordering = new PanelOrdering();
        ordering.Apply([(1, Zone1), (2, Zone1), (3, Zone1), (4, Zone1)], Dead);
        ordering.Apply([(1, Zone1), (3, Zone1), (4, Zone1)], w => w == 2); // visible: 1, 3, 4

        ordering.Move(Zone1, 4, 1);

        Assert.Equal([1, 4, 3], ordering.Get(Zone1));
    }
}
