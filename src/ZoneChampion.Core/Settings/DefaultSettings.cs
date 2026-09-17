namespace ZoneChampion.Core.Settings;

public static class DefaultSettings
{
    /// <summary>The settings file written on first run. Values must match the defaults in <see cref="PanelStyle"/>.</summary>
    public const string Text = """
        {
          // Zone Champion settings. Changes apply as soon as you save this file.
          // Sizes are in device-independent pixels, so they scale with Windows display scaling.
          // Colors are "#RRGGBB" or "#AARRGGBB".

          // Launch Zone Champion when you sign in to Windows.
          "startWithWindows": true,

          // Folder holding FancyZones' layout files. null = the default PowerToys location.
          "fancyZonesDataFolder": null,

          // Style shared by every panel. Any of these can be overridden for one zone under "zones" below.
          "panel": {
            // ---- Placement ----
            // Which side of the zone the panel hugs: "auto", "left", "right", "top" or "bottom".
            // "auto" picks left or right, whichever side of the zone is nearer the edge of the screen.
            "edge": "auto",
            // Where along that side: "start", "center" or "end".
            "alignment": "center",
            // Gap between the zone's edge and the panel. Negative values push the panel into the gap between zones.
            "edgeOffset": 0,
            // Shift along the side (positive = down or right).
            "alignmentOffset": 0,
            // Hide a panel while its zone has no windows.
            "hideWhenEmpty": true,
            // Hide the panels on a monitor while a full-screen app (video, game, slideshow) is in front.
            "hideOnFullscreen": true,

            // ---- Panel body ----
            "background": "#1E1E2E",
            "backgroundOpacity": 0.85,
            "borderColor": "#FFFFFF",
            "borderOpacity": 0.10,
            "borderThickness": 1,
            "cornerRadius": 14,
            "padding": 5,
            "shadow": true,
            "shadowOpacity": 0.35,
            "shadowSize": 12,

            // ---- Icons ----
            "iconSize": 32,
            // Space between an icon and the edge of its highlight tile.
            "itemPadding": 7,
            // Space between tiles.
            "itemSpacing": 2,
            "itemCornerRadius": 10,
            "hoverColor": "#FFFFFF",
            "hoverOpacity": 0.10,
            // Tile color for the window that currently has focus.
            "activeColor": "#89B4FA",
            "activeOpacity": 0.20,
            // A small bar on the screen-edge side of the focused window's tile.
            "activeIndicator": true,
            "activeIndicatorColor": "#89B4FA",
            // Icon opacity for minimized windows.
            "minimizedOpacity": 0.45,

            // ---- Hover title and right-click menu ----
            "showTitleOnHover": true,
            "titleDelayMs": 250,
            "popupBackground": "#1E1E2E",
            "popupBackgroundOpacity": 0.97,
            "popupForeground": "#CDD6F4",
            "fontFamily": "Segoe UI Variable Text, Segoe UI",
            "fontSize": 13,

            // ---- Color tags (right-click an icon to pick one) ----
            // "dot" = small dot on the icon, "tile" = tint the icon's tile.
            "tagStyle": "dot",
            "tagSize": 9,
            "tagTileOpacity": 0.35,
            "tagColors": ["#F38BA8", "#FAB387", "#F9E2AF", "#A6E3A1", "#94E2D5", "#89B4FA", "#CBA6F7"]
          },

          // Per-zone overrides, applied in order. Each entry can have:
          //   "monitor": FancyZones monitor id such as "RKU0550" (leave out to match every monitor)
          //   "zone":    zone number as shown in the FancyZones editor, starting at 1 (leave out to match every zone)
          //   "enabled": false to turn the panel off
          //   plus any "panel" setting from above.
          // Right-click the tray icon and choose "Identify zones" to see each zone's monitor id and number.
          "zones": [
            // { "monitor": "RKU0550", "zone": 1, "edge": "left", "background": "#302434" },
            // { "monitor": "AUO75AC", "enabled": false }
          ]
        }
        """;
}
