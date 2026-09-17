# Zone Champion: a taskbar for every FancyZones zone

**Floating, KDE Plasma-style taskbar panels for Windows 11, built as a companion app for PowerToys FancyZones.**

Zone Champion gives every FancyZones zone its own small taskbar. Each panel sits on the zone's outer edge and shows only the windows that are in that zone right now. Drag a window into another zone and its icon moves to that zone's panel.

It's built for big screens split into virtual monitors: a 4K TV used as a monitor, an ultrawide or super ultrawide display, or any large monitor divided into quadrants or columns. On those setups, the single Windows taskbar at the bottom is a long way from the window you're looking for. Zone Champion expands the taskbar instead of replacing it: the regular Windows taskbar stays exactly as it is.

## Why

Split a 50" TV into four FancyZones zones and you effectively have four 25" monitors, but still one taskbar. When three browser windows sit in the top-left zone and four VS Code windows in the bottom-left one, their icons are all mixed together at the bottom of the screen.

The Windows taskbar knows about physical monitors, not zones. Multi-monitor taskbars show a taskbar per physical display, which doesn't help when one display holds four "monitors". KDE Plasma on Linux solves this with multiple panels you can put on any edge. Zone Champion brings that idea to Windows: one taskbar panel per zone, placed on that zone's edge.

## Features

- **A taskbar per zone.** Each FancyZones zone gets its own panel, listing only the windows in that zone. Several taskbars on one monitor, one per screen region.
- **Live window tracking.** Icons follow windows between zones as you move, snap, minimize, maximize, open and close them.
- **Works with snapped and unsnapped windows.** A window belongs to the zone holding the largest part of it.
- **Taskbar-style clicks.** Click an icon to focus the window, and click again to minimize it.
- **Drag-and-drop reordering.** Put each window's icon where you'll remember it.
- **Tell identical windows apart.** Hover an icon to see the window title, or give a window a color tag from the right-click menu.
- **Move windows between zones** from the right-click menu, including zones on other monitors.
- **Hide all panels with a hotkey.** Tap Ctrl+Win (configurable) to hide them and tap again to bring them back.
- **Stays out of the way.** A monitor's panels hide while a maximized window is in front or a full-screen app, video or game is playing.
- **Customizable like KDE Plasma panels:**
  - Panel placement: left, right, top or bottom edge of each zone, including vertical taskbars on the side of a zone
  - Rounded corners, background color and transparency, borders and drop shadows
  - Icon size, spacing, hover and active-window highlights
  - Fonts and popup colors
  - Per-zone and per-monitor overrides
  - All in a JSON settings file that applies the moment you save
- **Reads FancyZones layouts directly.** It supports grid, priority grid, columns, rows, focus, custom grid and canvas layouts, and updates as soon as you change a layout in the FancyZones editor.
- **Multi-monitor and mixed DPI.** Monitors with different scaling (for example a 100% TV next to a 125% laptop screen) line up to the pixel.
- **Virtual desktop aware.** Panels show only windows on the current virtual desktop.
- **Lightweight.** A single ~500 KB executable using near-zero CPU while idle. It never steals focus and runs from the system tray.

## Who it's for

- People using a **TV as a computer monitor** and splitting it into zones.
- **Ultrawide and super ultrawide monitor** users (21:9, 32:9, 49-inch) who divide the screen with FancyZones.
- **PowerToys FancyZones** users who want a **FancyZones taskbar**: an add-on that shows which windows are in which zone.
- **KDE Plasma users on Windows** who miss multiple panels, per-edge panels and panel theming.
- Anyone who wants **multiple taskbars on a single monitor**, a **taskbar per virtual monitor**, or a **second taskbar** for a region of the screen.

## FAQ

**Can Windows 11 show more than one taskbar on the same monitor?**
Not by itself. The Windows taskbar is one per physical monitor. Zone Champion adds a small taskbar for each FancyZones zone, so one monitor can have two, four or more.

**Does FancyZones have a taskbar for each zone?**
No. FancyZones arranges windows but doesn't show which windows are in which zone. Zone Champion is a companion app that reads your FancyZones layouts and adds that per-zone taskbar.

**Is there a KDE Plasma panel for Windows?**
Zone Champion recreates Plasma-style panels for task switching: floating panels on any edge, with rounded corners, transparency, colors and per-panel settings. It currently has one widget, the window list, with no clock, launcher or system tray widgets.

**Does it replace the Windows taskbar?**
No. It's a taskbar extension: the Start menu, system tray, clock and regular taskbar are untouched.

**Does it work without PowerToys?**
No. Zones come from PowerToys FancyZones, so you need PowerToys with a FancyZones layout applied.

**Can the panel be vertical, or sit on the side of a zone?**
Yes. By default, panels go on the left or right edge of each zone, whichever is closer to the edge of the screen. The `edge` setting can put a panel on any side of any zone.

**Is it a DisplayFusion alternative?**
If what you want from monitor splitting is a separate taskbar for each region of a large screen, Zone Champion does that using the FancyZones layouts you already have.

## Requirements

- Windows 11 (also targets Windows 10)
- Microsoft PowerToys, with FancyZones enabled and a layout applied to your monitors
- .NET 10 Desktop Runtime (included with the .NET 10 SDK)

## Install

Build from source with the .NET 10 SDK:

```powershell
.\tools\Publish.ps1 -Install                   # build, copy to %LOCALAPPDATA%\Programs\ZoneChampion and start it
.\tools\Publish.ps1                            # just build a single ZoneChampion.exe into .\publish
dotnet run --project src/ZoneChampion          # run a debug build straight from the repo
dotnet test                                    # unit tests for zone math, assignment, ordering, hotkey, settings
```

Release builds add themselves to startup (HKCU `Run` key) while `"startWithWindows"` is `true`. Debug builds never touch startup.

## Using it

| Action | Result |
|---|---|
| Click an icon | Focuses that window, or minimizes it if it already has focus |
| Drag an icon along the panel | Reorders it |
| Hover an icon | Shows the window title |
| Right-click an icon | Color tag, move the window to another zone, close the window |
| Tap **Ctrl+Win** (press both, release both) | Hides every panel, for example to reach controls a panel covers. Tap again to bring them back. |

The tray icon's menu has:
- **Identify zones:** labels every zone with its monitor id and zone number, which you use in the settings file. Double-clicking the tray icon does the same.
- **Hide panels / Show panels:** the same toggle as the hotkey. While panels are hidden, the tray tooltip says so.
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
- A monitor's panels hide while its frontmost window is maximized, since that window covers every zone. They return when a normal window comes to the front or the maximized window is restored or minimized. This is `hideWhenMaximized`.

## Settings

Settings live in `%APPDATA%\ZoneChampion\settings.json`, which is created with every option and a comment for each on first run.
- **Saving:** changes apply as soon as you save.
- **Errors:** if the file has a mistake, such as a typo'd setting name or a bad color, the previous settings stay in effect. The tray shows the problem and it's written to the log.

Everything under `"panel"` applies to all panels:

| Group | Settings |
|---|---|
| Placement | `edge` (`auto`, `left`, `right`, `top`, `bottom`), `alignment` (`start`, `center`, `end`), `edgeOffset`, `alignmentOffset`, `hideWhenEmpty`, `hideOnFullscreen`, `hideWhenMaximized` |
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

`"toggleHotkey"` sets the keys that hide and show the panels. The default is `"Ctrl+Win"`, and `null` turns the hotkey off.
- **Allowed keys:** modifier keys only, joined with `+`: `Ctrl`, `Alt`, `Shift` and `Win`, or one side only with `LCtrl`, `RCtrl`, `LAlt`, `RAlt`, `LShift`, `RShift`, `LWin` and `RWin`. Holding modifiers types nothing, so the hotkey never leaks into the app you're using.
- **When it counts:** only when the keys are pressed and released with no other key in between. Shortcuts such as Ctrl+Win+Left still work normally.
- **Start menu:** Zone Champion sends a no-op key press so letting go of Win doesn't open the Start menu.

`"fancyZonesDataFolder"` overrides where the FancyZones layout files are read from. The default is `%LOCALAPPDATA%\Microsoft\PowerToys\FancyZones`.

## Troubleshooting

- **Log:** `%APPDATA%\ZoneChampion\zonechampion.log`
- **Diagnostics:** `ZoneChampion.exe --diagnose [file]` writes a report without showing any panels, to `%APPDATA%\ZoneChampion\diagnostics.txt` by default. The report lists monitors and their FancyZones ids, the layout and zone rectangles for each monitor, which edge each panel uses, and every window with the zone it was assigned to.
- **Snapshots:** `ZoneChampion.exe --snapshot <folder> [seconds]` starts normally. After the delay (3 seconds by default), it saves each panel and one right-click menu as PNGs, plus `panels.txt` with panel positions and contents, then exits. It renders only Zone Champion's own visuals.

## Known limitations

- Panels float over the edge of the windows in their zone. They don't reserve space, but the toggle hotkey hides them when something underneath needs a click.
- Windows running as administrator can't be minimized, moved or closed from the panel unless Zone Champion also runs as administrator. Windows blocks lower-privilege apps from controlling them. For the same reason, the toggle hotkey doesn't register while an administrator window has focus.
- FancyZones' "span zones across monitors" mode isn't supported. Each monitor's layout is read separately.
- Icons can't be dragged from one panel to another. Use right-click, then **Move to**.
- The window list is the only panel widget. There's no clock, launcher or system tray widget.

## How it works

| Piece | Where |
|---|---|
| FancyZones file parsing and a port of its zone math (grid, priority-grid, columns, rows, focus, canvas) | `src/ZoneChampion.Core/FancyZones` |
| Largest-overlap zone assignment and maximized-window coverage | `src/ZoneChampion.Core/Zones` |
| Icon order, panel placement, settings parsing and per-zone merging | `src/ZoneChampion.Core/Panels`, `src/ZoneChampion.Core/Settings` |
| Toggle hotkey parsing and tap detection | `src/ZoneChampion.Core/Input` |
| Win32 interop: monitors, virtual desktops, window events, window actions, keyboard hook | `src/ZoneChampion/Interop` |
| Window list (throttled rescans on window events) and icon loading | `src/ZoneChampion/Tracking` |
| Panel windows, theme, right-click menu, zone labels | `src/ZoneChampion/Panels` |
| Startup wiring, file watchers, tray | `src/ZoneChampion/AppController.cs`, `TrayIcon.cs` |

- **Zone math:** the zone calculations mirror PowerToys 0.100's `LayoutConfigurator.cpp` exactly, including integer rounding, so panel edges land on the same pixels as FancyZones' zones. Unit tests cover it against a real layout.
- **Coordinates:** the app runs per-monitor DPI aware and positions everything in physical pixels, so mixed-scaling setups line up.
- **Focus:** panels never take focus. That's what lets "click the focused window's icon to minimize" work.
- **Stack:** C#, .NET 10 and WPF, using WinEvent hooks for window tracking and a low-level keyboard hook for the hotkey.

`tools/New-AppIcon.ps1` regenerates `src/ZoneChampion/Assets/ZoneChampion.ico`.

---

**Related searches:** taskbar per zone, FancyZones taskbar, FancyZones companion app, PowerToys FancyZones add-on, multiple taskbars on one monitor, second taskbar Windows 11, taskbar extension, task tray expansion, floating taskbar, vertical taskbar, side taskbar, KDE Plasma panel for Windows, Plasma-style panels on Windows, taskbar for virtual monitors, split screen taskbar, TV as monitor taskbar, ultrawide monitor taskbar, large monitor window management, window switcher by screen region, DisplayFusion alternative.
