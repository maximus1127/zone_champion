using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Panels;
using ZoneChampion.Core.Settings;

namespace ZoneChampion.Core.Tests;

public class PanelPlacementTests
{
    private static readonly RectI WorkArea = new(0, 0, 3840, 2112);

    [Fact]
    public void Auto_PicksSideNearestScreenEdge()
    {
        Assert.Equal(PanelEdge.Left, PanelPlacement.ResolveEdge(PanelEdge.Auto, new RectI(16, 16, 1957, 1050), WorkArea));
        Assert.Equal(PanelEdge.Right, PanelPlacement.ResolveEdge(PanelEdge.Auto, new RectI(1973, 16, 3824, 1050), WorkArea));
        Assert.Equal(PanelEdge.Left, PanelPlacement.ResolveEdge(PanelEdge.Auto, new RectI(960, 0, 2880, 2112), WorkArea));
        Assert.Equal(PanelEdge.Top, PanelPlacement.ResolveEdge(PanelEdge.Top, new RectI(1973, 16, 3824, 1050), WorkArea));
    }

    [Fact]
    public void LeftEdge_CenteredVertically_WithInsetCompensation()
    {
        var zone = new RectI(16, 16, 1016, 1016);
        var size = new SizeI(72, 200); // includes a 12 DIP inset on every side

        var origin = PanelPlacement.ComputeOrigin(zone, PanelEdge.Left, PanelAlignment.Center, size, 1.0, edgeOffset: 4, alignmentOffset: 0, inset: 12);

        // Visible panel starts 4 px inside the zone; the window starts 12 px earlier for the shadow.
        Assert.Equal(new PointI(16 + 4 - 12, 16 + (1000 - 200) / 2), origin);
    }

    [Fact]
    public void RightEdge_ScalesOffsetsByDpi()
    {
        var zone = new RectI(1000, 0, 2000, 1000);
        var size = new SizeI(90, 300);

        var origin = PanelPlacement.ComputeOrigin(zone, PanelEdge.Right, PanelAlignment.Start, size, 1.25, edgeOffset: 8, alignmentOffset: 20, inset: 12);

        Assert.Equal(new PointI(2000 - 10 - 90 + 15, 0 - 15 + 25), origin);
    }

    [Fact]
    public void BottomEdge_EndAlignment()
    {
        var zone = new RectI(0, 0, 1000, 500);
        var size = new SizeI(300, 60);

        var origin = PanelPlacement.ComputeOrigin(zone, PanelEdge.Bottom, PanelAlignment.End, size, 1.0, 0, 0, 10);

        Assert.Equal(new PointI(1000 - 300 + 10, 500 - 60 + 10), origin);
    }
}
