# CoTW Need Zone Overlay (C# / WPF)

A native Windows overlay for theHunter: Call of the Wild that lists every species on a map with its **current need** and **next need**, each with start–end times. It's styled to match UI option A: rounded corners, smooth edges, Inter and Barlow Condensed fonts, dark and light themes.

It doesn't modify or read the game. You pick the map and enter the in-game time yourself.

## One-time setup

1. Install the **.NET 8 SDK** (x64): https://dotnet.microsoft.com/download/dotnet/8.0
2. Optional, for code help in VS Code: install the **C# Dev Kit** extension.
3. In the game: Settings → Display → **Borderless windowed**. Exclusive fullscreen hides overlays.

## Build the exe

Double-click **`build.bat`**, or in VS Code open this folder and press **Ctrl+Shift+B** ("Publish exe").

The result is `publish\CotwNeedOverlay.exe`. Keep the `publish\data` folder and `icon.ico` next to it. The fonts are built into the exe.

To try changes quickly without publishing, run `run.bat`, or in VS Code run the task **Terminal → Run Task → Run overlay**.

If the build fails, copy the error lines from the terminal and send them to Claude.

## Using it

1. **Select Map** → pick the map you're hunting on.
2. Click **⏱** and type the in-game time as digits. The `:` and a leading `0` are added for you (`720` → `07:20`).
3. Press **Ctrl+Alt+I** for click-through so the overlay never takes your clicks. Press it again to use the menus.

| Control | What it does |
|---|---|
| Select Map | Choose the map. The button always reads "Select Map"; the chosen map shows below the title bar. |
| Table | Each species' **Now** need and **Next** need, each with start–end times. |
| ☐ / ☑ | Select species to track. Select as many as you want. |
| Species name | Click to hide or show that species' times. |
| ⏱ | Enter the in-game time (24-hour). |
| Settings (sliders icon) | Theme (dark/light), font size (A− / A+), opacity (− / +), Collapse all / Expand all. |
| Show selected only | Bottom-left of the overlay: shows only the species you've ticked. |
| ▴ / ▾ | Compact mode: the title bar shows just the in-game time (centred) and the icons, with only your selected species and their times below. ▾ expands again. |
| ◢ (bottom-right) | Drag to resize. Double-click to fit the contents again. |
| Drag the title bar | Move the overlay. |
| Tray icon | Shows the app is running. Left-click: show/hide. Right-click: click-through, time sync, exit. |
| Ctrl+Alt+H / I / T | Show/hide · click-through · enter the in-game time. |

Settings are saved to `%APPDATA%\CotwNeedOverlay\settings-wpf.json`. That's where you change hotkeys (e.g. `"ctrl+shift+f9"`) or the clock speed (`real_seconds_per_game_minute`, default 15 = 1 in-game hour per 15 real minutes).

## Data

Each map is one JSON file in `data\`, with need times from the community "COTW Zone Times" sheet (17 maps). Edit a file to fix a time, or copy one to add a map. Windows are `["HH:MM", "HH:MM", "Feed|Drink|Rest"]`, and the beaver also uses `Den`, `Swim` and `Gather`.

## Fonts

**Inter** (bundled) and **Barlow Condensed** (downloaded by `get_fonts.bat` during the build). Both are free fonts under the SIL Open Font License. If Barlow Condensed can't be downloaded, the title uses Windows' built-in Bahnschrift.

## Project layout

- `MainWindow.xaml` / `.cs`: the overlay (title bar, table, settings popup, tray, hotkeys, click-through)
- `SyncWindow.xaml` / `.cs`: the "Enter current time - 24h Format" box
- `App.xaml`: all the styles (buttons, checkboxes, menus, scrollbar, tooltips)
- `Schedule.cs`: need-time logic · `Settings.cs`: saved settings and game clock · `Theme.cs`: colors

## Updates from GitHub

The overlay can update itself from your GitHub repository's releases.

**One-time setup**
1. Create a **public** GitHub repository and put this project folder in it, so `NeedZones.csproj` sits at the top of the repo. In Visual Studio: **Git → Create Git Repository → GitHub**, and untick "Private".
2. In `Updater.cs`, set `Repo` to `"your-username/your-repo-name"`, then commit and push.

**Publishing an update**
1. Commit and push your changes.
2. Create a tag that starts with `v` and is higher than the last one (e.g. `v1.0.1`) and push it. In Visual Studio: **Git → Manage Branches**, right-click the latest commit → **New Tag**, then push with tags.
   From a terminal: `git tag v1.0.1` and then `git push origin v1.0.1`.
3. GitHub Actions (`.github/workflows/release.yml`) builds the app and posts a release with a `.zip`. Watch it under your repo's **Actions** tab. It takes about 2–3 minutes.

**In the app**
- Settings → **UPDATES** shows your version and a **Check for updates** button.
- The app also checks quietly a few seconds after starting. If there's a newer release, the settings icon gets an amber dot and the button reads **Update to vX.Y.Z**.
- Updating downloads the zip, replaces the exe, `data` folder and icon, then restarts. Your settings are kept (they live in `%APPDATA%`). Changes you made yourself to files in `data\` are replaced by the release's versions.

Publishing a release by hand instead: set `<Version>` in `NeedZones.csproj` to the new number, publish, zip the *contents* of the `publish` folder, then create a release on GitHub with tag `v` + that number and attach the zip. The version and the tag must match, or the app keeps offering the same update.
