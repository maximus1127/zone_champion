using System.Text.Json;
using ZoneChampion.Core.Settings;

namespace ZoneChampion.Core.Tests;

public class SettingsDocumentTests
{
    [Fact]
    public void DefaultFile_MatchesCodeDefaults()
    {
        var fromFile = SettingsDocument.Default.Resolve("ANY", "any", 1).Style;
        var fromCode = new PanelStyle();
        fromCode.Normalize();

        Assert.Equal(
            JsonSerializer.Serialize(fromCode, SettingsDocument.JsonOptions),
            JsonSerializer.Serialize(fromFile, SettingsDocument.JsonOptions));
        Assert.True(SettingsDocument.Default.StartWithWindows);
        Assert.Null(SettingsDocument.Default.FancyZonesDataFolder);
        Assert.Equal("Ctrl+Win", SettingsDocument.Default.ToggleHotkey?.ToString());
    }

    [Theory]
    [InlineData("{ }", "Ctrl+Win")]
    [InlineData("""{ "toggleHotkey": "Alt+Shift" }""", "Alt+Shift")]
    [InlineData("""{ "toggleHotkey": null }""", null)]
    [InlineData("""{ "toggleHotkey": "" }""", null)]
    public void ToggleHotkey_DefaultsWhenMissingAndCanBeTurnedOff(string json, string? expected)
    {
        Assert.Equal(expected, SettingsDocument.Parse(json).ToggleHotkey?.ToString());
    }

    [Fact]
    public void ZoneOverrides_LayerOverBaseStyle()
    {
        var settings = SettingsDocument.Parse("""
            {
              "panel": { "background": "#101010", "iconSize": 40 },
              "zones": [
                { "monitor": "RKU0550", "Background": "#202020" },
                { "monitor": "RKU0550", "zone": 2, "edge": "right", "enabled": false },
              ]
            }
            """);

        var zone1 = settings.Resolve("RKU0550", "inst", 1);
        var zone2 = settings.Resolve("RKU0550", "inst", 2);
        var otherMonitor = settings.Resolve("AUO75AC", "inst2", 2);

        Assert.True(zone1.Enabled);
        Assert.Equal("#202020", zone1.Style.Background);
        Assert.Equal(40, zone1.Style.IconSize);
        Assert.Equal(PanelEdge.Auto, zone1.Style.Edge);

        Assert.False(zone2.Enabled);
        Assert.Equal(PanelEdge.Right, zone2.Style.Edge);

        Assert.True(otherMonitor.Enabled);
        Assert.Equal("#101010", otherMonitor.Style.Background);
    }

    [Fact]
    public void MonitorCanBeMatchedByInstanceId()
    {
        var settings = SettingsDocument.Parse("""{ "zones": [ { "monitor": "5&2f3cbba3&0&UID4356", "enabled": false } ] }""");

        Assert.False(settings.Resolve("RKU0550", "5&2f3cbba3&0&UID4356", 3).Enabled);
    }

    [Theory]
    [InlineData("""{ "panel": { "backgroundColour": "#000" } }""", "unknown setting \"backgroundColour\"")]
    [InlineData("""{ "zones": [ { "zone": 1, "iconSize": "big" } ] }""", "zones[0]")]
    [InlineData("""{ "panel": { "background": "blue" } }""", "background: 'blue' is not a color")]
    [InlineData("""{ "panel": { "edge": "middle" } }""", "panel")]
    [InlineData("""{ "startWithWindows": "yes" }""", "startWithWindows: expected true or false")]
    [InlineData("""{ "zones": [ { "zone": 0 } ] }""", "zone numbers start at 1")]
    [InlineData("""{ "pannel": {} }""", "Unknown setting \"pannel\"")]
    [InlineData("""{ "panel": { """, "isn't valid JSON")]
    [InlineData("""{ "toggleHotkey": "Tab+Space" }""", "toggleHotkey: \"Tab\" isn't a modifier key")]
    public void InvalidSettings_ExplainTheProblem(string json, string expectedMessagePart)
    {
        var ex = Assert.Throws<SettingsException>(() => SettingsDocument.Parse(json));

        Assert.Contains(expectedMessagePart, ex.Message);
    }

    [Fact]
    public void OutOfRangeNumbers_AreClamped()
    {
        var style = SettingsDocument.Parse("""{ "panel": { "backgroundOpacity": 3, "iconSize": 2 } }""").Resolve("M", "M", 1).Style;

        Assert.Equal(1, style.BackgroundOpacity);
        Assert.Equal(12, style.IconSize);
    }

    [Theory]
    [InlineData("#FFF", 255, 255, 255, 255)]
    [InlineData("#1E1E2E", 255, 0x1E, 0x1E, 0x2E)]
    [InlineData("#801E1E2E", 0x80, 0x1E, 0x1E, 0x2E)]
    public void ParsesColors(string text, byte a, byte r, byte g, byte b)
    {
        Assert.Equal(new ColorValue(a, r, g, b), ColorValue.Parse(text));
    }
}
