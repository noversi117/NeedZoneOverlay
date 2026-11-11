# Live Need Zones

An overlay for **theHunter: Call of the Wild** that shows, for every species on your map, which need zone it's using **now** and which one is **next**, each with start and end times.

It sits on top of the game and never reads or changes any game files. You choose the map and enter the in-game time, and the overlay keeps the clock running from there.

## Install

1. Download the latest `.zip` from this repository's **Releases** page.
2. Extract it to a folder of your own, for example `C:\Games\LiveNeedZones`.
3. Run the `.exe`. Keep the `data` folder.

You need the free **.NET 8 Desktop Runtime (x64)**. If it's missing, Windows will offer a download link the first time you run the overlay. You can also get it from https://dotnet.microsoft.com/download/dotnet/8.0

In the game, set **Settings → Display → Borderless windowed**. Exclusive fullscreen hides overlays.

## Getting started

1. Click **Select Map** and choose the map you're hunting on.
2. Press **Ctrl+Alt+T** (or click the clock icon) and type the in-game time in 24-hour format. Just type the digits: `720` becomes `07:20`, `1905` becomes `19:05`.
3. Press **Ctrl+Alt+I** for click-through (LOCKED/UNLOCKED), so your mouse goes to the game instead of the overlay. Press it again when you want to use the overlay's buttons.

The overlay counts the in-game time forward on its own. Re-enter it after sleeping in a tent or lodge, after pausing, or if it drifts.

## Reading the table

Each row is one species:

- **Now**: the need zone it's using right now (**Feed**, **Drink** or **Rest**) and that window's start–end time. "no need zone" means it isn't at one.
- **Next**: the need zone that comes after, with its times.

## Controls

| Control | What it does |
|---|---|
| **Select Map** | Choose the map to show. The chosen map's name appears below the title bar. |
| Checkbox next to a species | Mark a species you're tracking. Tick as many as you like. |
| Species name | Click to hide or show that species' times. |
| **Show selected only** (bottom-left) | Show only the species you've ticked. |
| Clock icon | Enter the in-game time. |
| Settings icon (sliders) | Theme, font size, opacity, collapse/expand all times, and updates. |
| ▴ / ▾ | **Compact mode**: the title bar shows only the time, with just your ticked species below. Click ▾ to return to the full view. |
| ◢ (bottom-right corner) | Drag to resize. Double-click to fit the contents again. |
| Title bar | Drag to move the overlay. |
| ✕ | Close the overlay. |

### Settings

- **Theme**: Dark or Light.
- **Font size**: A− / A+.
- **Opacity**: − makes the overlay more see-through, + more solid.
- **Times**: Collapse all / Expand all.
- **Updates**: your version and a **Check for updates** button.

Your map, ticked species, theme, size, position and time are remembered between sessions.

## Hotkeys

These work anywhere, even while you're in the game:

| Hotkey | Action |
|---|---|
| **Ctrl+Alt+T** | Enter the in-game time |
| **Ctrl+Alt+I** | Switch between click-through and using the overlay |
| **Ctrl+Alt+H** | Show / hide the overlay |

## Tray icon

While the overlay is running, its leaf icon sits by the Windows clock. It may be hidden under the **^** arrow.

- **Left-click**: show or hide the overlay.
- **Right-click**: click-through, enter the time, or exit.

## Updates

A few seconds after starting, the overlay checks for a newer version. When one is available, the settings icon shows an **amber dot**. Open Settings and click **Update to vX.Y.Z**: the update downloads, installs and restarts the overlay. Your settings are kept.

You can also check at any time with **Settings → Check for updates**.

## Maps included

Hirschfelden, Layton Lake, Medved-Taiga, Vurhonga Savanna, Parque Fernando, Yukon Valley, Cuatro Colinas, Silver Ridge Peaks, Te Awaroa, Rancho del Arroyo, Mississippi Acres, Revontuli Coast, New England Mountains, Emerald Coast, Sundarpatan, Salzwiesen Park and Askiy Ridge.

Need times come from the community "COTW Zone Times" spreadsheet. Waterfowl and rabbit times are approximate. The North American Beaver (Askiy Ridge) shows **Den**, **Swim** and **Gather** instead of the usual needs.

## Advanced settings

Settings are stored in `%APPDATA%\CotwNeedOverlay\settings-wpf.json`. Close the overlay before editing it.

- **Hotkeys**: `hotkey_toggle_visible`, `hotkey_toggle_interact`, `hotkey_sync_clock`, e.g. `"ctrl+shift+f9"`.
- **Clock speed**: `real_seconds_per_game_minute` (default `15`, so 1 in-game hour = 15 real minutes). Change it if the overlay's time steadily drifts from the game's.
- **Tray icon**: set `tray_icon` to `false` to hide it.

## Troubleshooting

- **Nothing appears when I start it.** It may already be running: press **Ctrl+Alt+H**, or use the tray icon. If you get a message saying it's already running, end it in Task Manager and start it again.
- **The overlay doesn't show over the game.** Switch the game to borderless windowed.
- **Clicks don't do anything on the overlay.** It's in click-through mode. Press **Ctrl+Alt+I**.
- **"Hotkey in use" in the footer.** Another program uses one of the hotkeys. Pick a different one in the advanced settings.
- **The time is off.** Press **Ctrl+Alt+T** and enter the current in-game time again.
- **An update failed.** Make sure the overlay isn't in `Program Files`, or download the newest zip from the Releases page and extract it over your folder.

## Credits

Fonts: **Inter** and **Barlow Condensed**, both under the SIL Open Font License.