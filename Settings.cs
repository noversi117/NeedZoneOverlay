using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeedZones
{
    public sealed class PerReserve
    {
        [JsonPropertyName("selected")] public List<string> Selected { get; set; } = new List<string>();
        [JsonPropertyName("collapsed")] public List<string> Collapsed { get; set; } = new List<string>();
    }

    /// <summary>Saved to %APPDATA%\CotwNeedOverlay\settings-wpf.json.</summary>
    public sealed class Settings
    {
        [JsonPropertyName("x")] public double X { get; set; } = 40;
        [JsonPropertyName("y")] public double Y { get; set; } = 120;
        [JsonPropertyName("w")] public double? W { get; set; }
        [JsonPropertyName("h")] public double? H { get; set; }
        [JsonPropertyName("alpha")] public double Alpha { get; set; } = 0.92;
        [JsonPropertyName("text_size")] public int TextSize { get; set; } = 13;
        [JsonPropertyName("theme")] public string Theme { get; set; } = "dark";
        [JsonPropertyName("minimized")] public bool Minimized { get; set; }
        [JsonPropertyName("reserve")] public string Reserve { get; set; } = "";
        [JsonPropertyName("show_selected_only")] public bool ShowSelectedOnly { get; set; }
        [JsonPropertyName("per_reserve")] public Dictionary<string, PerReserve> PerReserve { get; set; } = new Dictionary<string, PerReserve>();
        [JsonPropertyName("clock_sync_game_min")] public double? ClockSyncGameMin { get; set; }
        [JsonPropertyName("clock_sync_real_utc")] public DateTime? ClockSyncRealUtc { get; set; }
        [JsonPropertyName("real_seconds_per_game_minute")] public double RealSecondsPerGameMinute { get; set; } = 15.0;
        [JsonPropertyName("start_interactive")] public bool StartInteractive { get; set; } = true;
        [JsonPropertyName("tray_icon")] public bool TrayIcon { get; set; } = true;
        [JsonPropertyName("hotkey_toggle_visible")] public string HotkeyToggleVisible { get; set; } = "ctrl+alt+h";
        [JsonPropertyName("hotkey_toggle_interact")] public string HotkeyToggleInteract { get; set; } = "ctrl+alt+i";
        [JsonPropertyName("hotkey_sync_clock")] public string HotkeySyncClock { get; set; } = "ctrl+alt+t";

        public const int TextMin = 10, TextMax = 20;

        public static string ConfigDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CotwNeedOverlay");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static string FilePath => Path.Combine(ConfigDir, "settings-wpf.json");

        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new Settings();
                    if (s.Theme != "dark" && s.Theme != "light") s.Theme = "dark";
                    s.TextSize = Math.Clamp(s.TextSize, TextMin, TextMax);
                    s.PerReserve ??= new Dictionary<string, PerReserve>();
                    // the time hotkey moved from Ctrl+Alt+S to Ctrl+Alt+T
                    if (string.Equals(s.HotkeySyncClock, "ctrl+alt+s", StringComparison.OrdinalIgnoreCase))
                        s.HotkeySyncClock = "ctrl+alt+t";
                    return s;
                }
            }
            catch (Exception)
            {
                // a broken settings file just means defaults
            }
            return new Settings();
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options)); }
            catch (Exception) { }
        }

        public PerReserve For(string reserveId)
        {
            if (!PerReserve.TryGetValue(reserveId, out var pr) || pr == null)
            {
                pr = new PerReserve();
                PerReserve[reserveId] = pr;
            }
            pr.Selected ??= new List<string>();
            pr.Collapsed ??= new List<string>();
            return pr;
        }
    }

    /// <summary>In-game time: counts on from the last sync at the game's clock rate.</summary>
    public sealed class GameClock
    {
        readonly Settings s;
        public GameClock(Settings settings) { s = settings; }

        public double? Now()
        {
            if (s.ClockSyncGameMin == null || s.ClockSyncRealUtc == null) return null;
            double rate = Math.Max(0.1, s.RealSecondsPerGameMinute);
            double elapsed = (DateTime.UtcNow - s.ClockSyncRealUtc.Value).TotalSeconds / rate;
            return ((s.ClockSyncGameMin.Value + elapsed) % Schedule.Day + Schedule.Day) % Schedule.Day;
        }

        public void Sync(int gameMinute)
        {
            s.ClockSyncGameMin = gameMinute;
            s.ClockSyncRealUtc = DateTime.UtcNow;
        }
    }
}
