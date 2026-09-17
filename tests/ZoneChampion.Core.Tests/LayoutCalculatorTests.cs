using ZoneChampion.Core.FancyZones;
using ZoneChampion.Core.Geometry;

namespace ZoneChampion.Core.Tests;

public class LayoutCalculatorTests
{
    private static FzLayout Template(string type, int zoneCount, int spacing = 16, bool showSpacing = true) =>
        new(type, Guid.Empty, showSpacing, spacing, zoneCount);

    [Fact]
    public void PriorityGridThreeZones_MatchesFancyZones()
    {
        // Worked example from the FancyZones source: default layout on a 1920x1040 work area.
        var zones = LayoutCalculator.Calculate(Template("priority-grid", 3), null, 1920, 1040, 96);

        Assert.Equal(
            [
                (0, new RectI(16, 16, 472, 1024)),
                (1, new RectI(488, 16, 1432, 1024)),
                (2, new RectI(1448, 16, 1904, 1024)),
            ],
            zones);
    }

    [Fact]
    public void CustomGrid_TwoByTwoWithUnevenSplits()
    {
        // The 2x2 layout applied to the 4K TV: cell-child-map numbers zones down each column.
        var custom = new FzCustomLayout(
            Guid.NewGuid(),
            "Custom layout 1",
            "grid",
            new FzGridInfo(2, 2, [5014, 4986], [5119, 4881], [[0, 2], [1, 3]], ShowSpacing: true, Spacing: 16),
            null);

        var zones = LayoutCalculator.Calculate(Template("custom", 4), custom, 3840, 2112, 96);

        Assert.Equal(
            [
                (0, new RectI(16, 16, 1957, 1050)),
                (1, new RectI(16, 1066, 1957, 2096)),
                (2, new RectI(1973, 16, 3824, 1050)),
                (3, new RectI(1973, 1066, 3824, 2096)),
            ],
            zones);
    }

    [Fact]
    public void Columns_EdgesAddUpToWorkArea()
    {
        var zones = LayoutCalculator.Calculate(Template("columns", 3), null, 1920, 1040, 96);

        Assert.Equal(
            [
                (0, new RectI(16, 16, 634, 1024)),
                (1, new RectI(650, 16, 1269, 1024)),
                (2, new RectI(1285, 16, 1904, 1024)),
            ],
            zones);
    }

    [Fact]
    public void Rows_StackVertically()
    {
        var zones = LayoutCalculator.Calculate(Template("rows", 2), null, 1000, 1000, 96);

        Assert.Equal(
            [
                (0, new RectI(16, 16, 984, 492)),
                (1, new RectI(16, 508, 984, 984)),
            ],
            zones);
    }

    [Fact]
    public void GridTemplate_LastZoneFillsRemainingCells()
    {
        // 5 zones -> 2 rows x 3 columns, map [[0,1,2],[3,4,4]].
        var zones = LayoutCalculator.Calculate(Template("grid", 5, spacing: 0), null, 3000, 2000, 96);

        Assert.Equal(5, zones.Count);
        Assert.Equal((3, new RectI(0, 1000, 999, 2000)), zones[3]);
        Assert.Equal((4, new RectI(999, 1000, 3000, 2000)), zones[4]);
    }

    [Fact]
    public void PriorityGrid_ElevenZonesFallsBackToGrid()
    {
        var zones = LayoutCalculator.Calculate(Template("priority-grid", 11, spacing: 0), null, 4000, 3000, 96);

        // Grid for 11 zones is 3 rows x 4 columns (row percents 3333/3333/3334), so the first zone is one cell.
        Assert.Equal(11, zones.Count);
        Assert.Equal((0, new RectI(0, 0, 1000, 999)), zones[0]);
    }

    [Fact]
    public void SpacingIgnoredWhenHidden()
    {
        var zones = LayoutCalculator.Calculate(Template("columns", 2, spacing: 16, showSpacing: false), null, 1000, 500, 96);

        Assert.Equal([(0, new RectI(0, 0, 500, 500)), (1, new RectI(500, 0, 1000, 500))], zones);
    }

    [Fact]
    public void Canvas_ScalesToWorkAreaAndDpi()
    {
        var custom = new FzCustomLayout(
            Guid.NewGuid(),
            "canvas",
            "canvas",
            null,
            new FzCanvasInfo(1920, 1080, [new FzCanvasZone(960, 0, 960, 540)]));

        // 125% scaling: 2400x1350 physical is 1920x1080 in 96-DPI units, so the zone scales by exactly 1.25.
        var zones = LayoutCalculator.Calculate(Template("custom", 1), custom, 2400, 1350, 120);

        Assert.Equal([(0, new RectI(1200, 0, 2400, 675))], zones);
    }

    [Fact]
    public void Focus_CascadesZones()
    {
        var zones = LayoutCalculator.Calculate(Template("focus", 2, spacing: 0), null, 1000, 1000, 96);

        Assert.Equal([(0, new RectI(100, 100, 500, 500)), (1, new RectI(150, 150, 550, 550))], zones);
    }

    [Theory]
    [InlineData("blank", 0)]
    [InlineData("columns", 0)]
    [InlineData("something-new", 3)]
    public void NoZones_ForBlankInvalidOrUnknownLayouts(string type, int zoneCount)
    {
        Assert.Empty(LayoutCalculator.Calculate(Template(type, zoneCount), null, 1920, 1080, 96));
    }

    [Fact]
    public void CustomGrid_WithMismatchedShape_HasNoZones()
    {
        var custom = new FzCustomLayout(Guid.NewGuid(), "bad", "grid", new FzGridInfo(2, 2, [10000], [5000, 5000], [[0, 1], [2, 3]], true, 16), null);

        Assert.Empty(LayoutCalculator.Calculate(Template("custom", 4), custom, 1920, 1080, 96));
    }
}
