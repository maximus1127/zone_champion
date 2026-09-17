# Zone Champion

Floating, KDE-style panels for PowerToys FancyZones. Each zone gets its own small taskbar that sits on the zone's outer edge and lists only the windows currently in that zone. Move a window to another zone and its icon moves with it.

Built for large displays split into several monitor-sized zones, where the one Windows taskbar at the bottom is a long way from the window you're looking for. The regular taskbar is left alone; this only adds to it.

## Requirements

- Windows 10 or 11
- PowerToys with FancyZones and a layout applied to your monitors
- .NET 10 Desktop Runtime (included with the .NET 10 SDK)

## Build and run

```powershell
dotnet run --project src/ZoneChampion          # debug build, runs from the repo
dotnet test                                    # unit tests for zone math, assignment, ordering, settings
.\tools\Publish.ps1                            # release build in .\publish (single ZoneChampion.exe)
.\tools\Publish.ps1 -Install                   # also copy to %LOCALAPPDATA%\Programs\ZoneChampion and start it
```

Release builds add themselves to startup (HKCU `Run` key) while `"startWithWindows"` is `true`. Debug builds never touch startup.

## Using it

| Action | Result |
|---|---|
| Click an icon | Focuses that window, or minimizes it if it already has focus |
| Drag an icon along the panel | Reorders it |
| Hover an icon | Shows the window title |
| Right-click an icon | Color tag, move the window to another zone, close the window |

The tray icon's menu has:
- **Identify zones:** labels every zone with its monitor id and zone number, which you use in the settings file. Double-clicking the tray icon does the same.
- **Hide panels / Show panels**
- **Edit settings**
- **Open settings folder**
- **Reload**
- **Exit**

Icon order and color tags last until Zone Champion restarts.

### Which panel a window goes to

- A window belongs to the zone that holds the largest part of it. Windows don't need to be snapped.
- If a window is split exactly evenly between zones, it stays where it was.
- A minimized window stays in the panel of the zone it was minimized from.
- A maximized window stays in the zone it was in before it was maximized.
- Windows outside every zone, and windows on other virtual desktops, aren't shown.
- A panel only appears when its zone has windows. This is configurable.

## Settings

Settings live in `%APPDATA%\ZoneChampion\settings.json`, which is created with every option and a comment for each on first run.
- **Saving:** changes apply as soon as you save.
- **Errors:** if the file has a mistake, such as a typo'd setting name or a bad color, the previous settings stay in effect. The tray shows the problem and it's written to the log.

Everything under `"panel"` applies to all panels:

| Group | Settings |
|---|---|
| Placement | `edge` (`auto`, `left`, `right`, `top`, `bottom`), `alignment` (`start`, `center`, `end`), `edgeOffset`, `alignmentOffset`, `hideWhenEmpty`, `hideOnFullscreen` |
| Panel body | `background`, `backgroundOpacity`, `borderColor`, `borderOpacity`, `borderThickness`, `cornerRadius`, `padding`, `shadow`, `shadowOpacity`, `shadowSize` |
| Icons | `iconSize`, `itemPadding`, `itemSpacing`, `itemCornerRadius`, `hoverColor`, `hoverOpacity`, `activeColor`, `activeOpacity`, `activeIndicator`, `activeIndicatorColor`, `minimizedOpacity` |
| Title and menu | `showTitleOnHover`, `titleDelayMs`, `popupBackground`, `popupBackgroundOpacity`, `popupForeground`, `fontFamily`, `fontSize` |
| Color tags | `tagStyle` (`dot` or `tile`), `tagSize`, `tagTileOpacity`, `tagColors` |

- **Sizes** are in device-independent pixels, so they follow Windows display scaling.
- **Colors** are `#RRGGBB` or `#AARRGGBB`.
- **`edge: "auto"`** puts the panel on the left or right side of the zone, whichever is nearer the edge of the screen.

`"zones"` holds per-zone overrides, applied in order. Each entry can have:
- `"monitor"`: the FancyZones monitor id, such as `RKU0550`. Leave it out to match every monitor.
- `"zone"`: the zone number from the FancyZones editor, starting at 1. Leave it out to match every zone.
- `"enabled": false`, to turn the panel off.
- Any `"panel"` setting.

```jsonc
"zones": [
  { "monitor": "RKU0550", "zone": 1, "background": "#302434", "cornerRadius": 20 },
  { "monitor": "RKU0550", "zone": 3, "edge": "top" },
  { "monitor": "AUO75AC", "enabled": false }
]
```

`"fancyZonesDataFolder"` overrides where the FancyZones layout files are read from. The default is `%LOCALAPPDATA%\Microsoft\PowerToys\FancyZones`.

## Troubleshooting

- **Log:** `%APPDATA%\ZoneChampion\zonechampion.log`
- **Diagnostics:** `ZoneChampion.exe --diagnose [file]` writes a report without showing any panels, to `%APPDATA%\ZoneChampion\diagnostics.txt` by default. The report lists monitors and their FancyZones ids, the layout and zone rectangles for each monitor, which edge each panel uses, and every window with the zone it was assigned to.
- **Snapshots:** `ZoneChampion.exe --snapshot <folder> [seconds]` starts normally. After the delay (3 seconds by default), it saves each panel and one right-click menu as PNGs, plus `panels.txt` with panel positions and contents, then exits. It renders only Zone Champion's own visuals.

## Known limitations

- Panels float over the edge of the windows in their zone. They don't reserve space.
- Windows running as administrator can't be minimized, moved or closed from the panel unless Zone Champion also runs as administrator. Windows blocks lower-privilege apps from controlling them.
- FancyZones' "span zones across monitors" mode isn't supported. Each monitor's layout is read separately.
- Icons can't be dragged from one panel to another. Use right-click, then **Move to**.

## How it works

| Piece | Where |
|---|---|
| FancyZones file parsing and a port of its zone math (grid, priority-grid, columns, rows, focus, canvas) | `src/ZoneChampion.Core/FancyZones` |
| Largest-overlap zone assignment | `src/ZoneChampion.Core/Zones/ZoneAssigner.cs` |
| Icon order, panel placement, settings parsing and per-zone merging | `src/ZoneChampion.Core/Panels`, `src/ZoneChampion.Core/Settings` |
| Win32 interop: monitors, virtual desktops, window events, window actions | `src/ZoneChampion/Interop` |
| Window list (throttled rescans on window events) and icon loading | `src/ZoneChampion/Tracking` |
| Panel windows, theme, right-click menu, zone labels | `src/ZoneChampion/Panels` |
| Startup wiring, file watchers, tray | `src/ZoneChampion/AppController.cs`, `TrayIcon.cs` |

- **Zone math:** the zone calculations mirror PowerToys 0.100's `LayoutConfigurator.cpp` exactly, including integer rounding, so panel edges land on the same pixels as FancyZones' zones. Unit tests cover it against a real layout.
- **Coordinates:** the app runs per-monitor DPI aware and positions everything in physical pixels, so mixed-scaling setups line up.
- **Focus:** panels never take focus. That's what lets "click the focused window's icon to minimize" work.

`tools/New-AppIcon.ps1` regenerates `src/ZoneChampion/Assets/ZoneChampion.ico`.
