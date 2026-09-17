using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Tests;

public class MonitorCoverageTests
{
    private static StackedWindow Normal(string monitor) => new(monitor, false, false);

    private static StackedWindow Maximized(string monitor) => new(monitor, false, true);

    private static StackedWindow Minimized(string monitor) => new(monitor, true, false);

    [Fact]
    public void FrontmostMaximizedWindowCoversItsMonitor()
    {
        var covered = MonitorCoverage.CoveredByMaximizedWindow([Maximized("laptop"), Normal("tv")]);

        Assert.Equal(["laptop"], covered);
    }

    [Fact]
    public void NormalWindowInFrontOfMaximizedOneUncoversTheMonitor()
    {
        Assert.Empty(MonitorCoverage.CoveredByMaximizedWindow([Normal("tv"), Maximized("tv")]));
    }

    [Fact]
    public void MinimizedWindowsAreSkipped()
    {
        var covered = MonitorCoverage.CoveredByMaximizedWindow([Minimized("tv"), Maximized("tv"), Normal("tv")]);

        Assert.Equal(["tv"], covered);
    }

    [Fact]
    public void EachMonitorIsDecidedByItsOwnFrontmostWindow()
    {
        var covered = MonitorCoverage.CoveredByMaximizedWindow(
            [Normal("tv"), Maximized("laptop"), Maximized("tv"), Normal("laptop")]);

        Assert.Equal(["laptop"], covered);
    }

    [Fact]
    public void WindowsOnUnknownMonitorsAreIgnored()
    {
        Assert.Empty(MonitorCoverage.CoveredByMaximizedWindow([new StackedWindow(null, false, true)]));
    }
}
