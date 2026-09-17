using ZoneChampion.Core.FancyZones;
using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Tests;

public class ZoneResolverTests
{
    private static readonly Guid Desktop = Guid.Parse("{88E35BA1-D00D-4387-A0E5-C1EC01099331}");

    // Trimmed copies of a real FancyZones setup: a 4K TV with a custom 2x2 grid and a laptop panel with priority-grid.
    private const string AppliedJson = """
        {
          "applied-layouts": [
            {
              "device": { "monitor": "RKU0550", "monitor-instance": "5&2f3cbba3&0&UID4356", "monitor-number": 6, "serial-number": "0", "virtual-desktop": "{88E35BA1-D00D-4387-A0E5-C1EC01099331}" },
              "applied-layout": { "uuid": "{7C4C1237-DB0C-4B35-A4D5-4EBC6B8E2448}", "type": "custom", "show-spacing": true, "spacing": 16, "zone-count": 4, "sensitivity-radius": 20 }
            },
            {
              "device": { "monitor": "AUO75AC", "monitor-instance": "5&278e36e4&0&UID256", "monitor-number": 1, "serial-number": "0", "virtual-desktop": "{88E35BA1-D00D-4387-A0E5-C1EC01099331}" },
              "applied-layout": { "uuid": "{00000000-0000-0000-0000-000000000000}", "type": "priority-grid", "show-spacing": true, "spacing": 16, "zone-count": 3, "sensitivity-radius": 20 }
            }
          ]
        }
        """;

    private const string CustomJson = """
        {
          "custom-layouts": [
            {
              "uuid": "{7C4C1237-DB0C-4B35-A4D5-4EBC6B8E2448}",
              "name": "Custom layout 1",
              "type": "grid",
              "info": { "rows": 2, "columns": 2, "rows-percentage": [5014, 4986], "columns-percentage": [5119, 4881], "cell-child-map": [[0, 2], [1, 3]], "show-spacing": true, "spacing": 16, "sensitivity-radius": 20 }
            }
          ]
        }
        """;

    private const string DefaultJson = """
        {
          "default-layouts": [
            { "monitor-configuration": "vertical", "layout": { "uuid": "", "type": "rows", "show-spacing": true, "spacing": 16, "zone-count": 3, "sensitivity-radius": 20 } },
            { "monitor-configuration": "horizontal", "layout": { "uuid": "", "type": "columns", "show-spacing": false, "spacing": 16, "zone-count": 2, "sensitivity-radius": 20 } }
          ]
        }
        """;

    private static readonly DisplayMonitor Tv = new(
        1, @"\\.\DISPLAY6", "RKU0550", "5&2f3cbba3&0&UID4356", 6,
        new RectI(0, 0, 3840, 2160), new RectI(0, 0, 3840, 2112), 96);

    private static readonly DisplayMonitor Laptop = new(
        2, @"\\.\DISPLAY1", "AUO75AC", "5&278e36e4&0&UID256", 1,
        new RectI(3840, 548, 5888, 1828), new RectI(3840, 548, 5888, 1780), 120);

    private static FancyZonesData Data => FancyZonesData.Parse(AppliedJson, CustomJson, DefaultJson);

    [Fact]
    public void ParsesRealFiles_WithoutWarnings()
    {
        var data = Data;

        Assert.Empty(data.Warnings);
        Assert.Equal(2, data.AppliedLayouts.Count);
        Assert.Single(data.CustomLayouts);
        Assert.Equal(2, data.DefaultLayouts.Count);
    }

    [Fact]
    public void ResolvesZonesInScreenCoordinates()
    {
        var zones = ZoneResolver.Resolve(Data, [Tv, Laptop], Desktop, Desktop);

        var tvZones = zones.Where(z => z.Monitor == Tv).ToList();
        var laptopZones = zones.Where(z => z.Monitor == Laptop).ToList();
        Assert.Equal(4, tvZones.Count);
        Assert.Equal(3, laptopZones.Count);

        Assert.Equal(new ZoneKey(Tv.InstanceId, 1), tvZones[1].Key);
        Assert.Equal(new RectI(16, 1066, 1957, 2096), tvZones[1].Bounds);

        // Laptop zones are offset by its work area origin.
        Assert.Equal(new RectI(3840 + 16, 548 + 16, 3840 + 504, 548 + 1216), laptopZones[0].Bounds);
    }

    [Fact]
    public void OtherVirtualDesktop_FallsBackToLastUsed()
    {
        var zones = ZoneResolver.Resolve(Data, [Tv], Guid.NewGuid(), Desktop);

        Assert.Equal(4, zones.Count);
    }

    [Fact]
    public void UnknownMonitor_UsesDefaultLayoutForItsOrientation()
    {
        var portrait = Tv with { DeviceId = "NEW0001", InstanceId = "x", MonitorNumber = 9, Bounds = new RectI(0, 0, 1080, 1920), WorkArea = new RectI(0, 0, 1080, 1872) };
        var landscape = portrait with { InstanceId = "y", Bounds = new RectI(0, 0, 1920, 1080), WorkArea = new RectI(0, 0, 1920, 1032) };

        Assert.Equal("rows", ZoneResolver.FindLayout(Data, portrait, Desktop, Desktop).Type);
        Assert.Equal("columns", ZoneResolver.FindLayout(Data, landscape, Desktop, Desktop).Type);
    }

    [Fact]
    public void InstanceChange_StillMatchesByMonitorNumber()
    {
        var replugged = Tv with { InstanceId = "5&aaaa&0&UID1" };

        Assert.Equal("custom", ZoneResolver.FindLayout(Data, replugged, Desktop, Desktop).Type);
    }
}
