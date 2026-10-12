using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Screens;
using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Tests;

public class ScreenSwapTests
{
    // Two 4K screens side by side, each with a 48 px taskbar at the bottom.
    private static readonly DisplayMonitor Side = new(1, "A", "A", "A", 1, new RectI(-3840, 0, 0, 2160), new RectI(-3840, 0, 0, 2112), 96);
    private static readonly DisplayMonitor Front = new(2, "B", "B", "B", 2, new RectI(0, 0, 3840, 2160), new RectI(0, 0, 3840, 2112), 96);

    // A 1080p screen with its taskbar on the left.
    private static readonly DisplayMonitor Small = new(3, "C", "C", "C", 3, new RectI(3840, 0, 5760, 1080), new RectI(3940, 0, 5760, 1080), 96);

    [Fact]
    public void SameSizeScreens_KeepExactPosition()
    {
        var zoned = new RectI(-3840, 0, -1920, 1056);

        Assert.Equal(new RectI(0, 0, 1920, 1056), ScreenSwap.Map(zoned, Side, Front));
        Assert.Equal(zoned, ScreenSwap.Map(ScreenSwap.Map(zoned, Side, Front), Front, Side));
    }

    [Fact]
    public void DifferentScreens_ScaleProportionallyWithinWorkArea()
    {
        // Right half of the front screen's work area.
        var rightHalf = new RectI(1920, 0, 3840, 2112);

        Assert.Equal(new RectI(4850, 0, 5760, 1080), ScreenSwap.Map(rightHalf, Front, Small));
    }

    [Fact]
    public void FullScreenWindow_CoversWholeTargetScreen()
    {
        Assert.Equal(Small.Bounds, ScreenSwap.Map(Front.Bounds, Front, Small));
    }

    [Fact]
    public void WindowMostlyOffItsScreen_IsPulledOntoTarget()
    {
        // Hangs off the side screen's left edge, into space with no monitor.
        var offEdge = new RectI(-4600, 100, -3640, 700);

        var mapped = ScreenSwap.Map(offEdge, Side, Front);

        Assert.Equal(new RectI(0, 100, 960, 700), mapped);
    }

    [Fact]
    public void WindowPartlyOffItsScreen_KeepsOverhang()
    {
        var overhang = new RectI(-400, 100, 200, 700); // mostly on the side screen, 200 px onto the front one

        Assert.Equal(new RectI(3440, 100, 4040, 700), ScreenSwap.Map(overhang, Side, Front));
    }

    [Fact]
    public void Owner_IsScreenHoldingMostOfTheWindow()
    {
        Assert.Equal(Side, ScreenSwap.Owner(new RectI(-400, 100, 200, 700), Side, Front));
        Assert.Equal(Front, ScreenSwap.Owner(new RectI(-200, 100, 400, 700), Side, Front));
        Assert.Null(ScreenSwap.Owner(new RectI(-32000, -32000, -31800, -31900), Side, Front));
    }
}
