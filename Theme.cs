using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace NeedZones
{
    /// <summary>Dark / light palettes, pushed into Application resources as brushes (DynamicResource in XAML).</summary>
    public sealed class Theme
    {
        public string Bg, Bg2, Fg, Dim, Dim2, Accent, Warn, Border, Line, HeadLine, Foot, Box, Hover;
        public Dictionary<string, string> Need;

        public static readonly Theme Dark = new Theme
        {
            Bg = "#14171b",
            Bg2 = "#232931",
            Fg = "#e6e6e6",
            Dim = "#a4abb4",
            Dim2 = "#8a9099",
            Accent = "#e8b04b",
            Warn = "#e07a5f",
            Border = "#3a424c",
            Line = "#1f252c",
            HeadLine = "#2b323a",
            Foot = "#1a1e23",
            Box = "#5b636d",
            Hover = "#1fffffff",
            Need = new Dictionary<string, string> { ["Feed"] = "#7cc36e", ["Drink"] = "#5fa8e8", ["Rest"] = "#d4af72" },
        };

        public static readonly Theme Light = new Theme
        {
            Bg = "#f6f3ec",
            Bg2 = "#e8e2d5",
            Fg = "#1c1f23",
            Dim = "#5f646b",
            Dim2 = "#6b7077",
            Accent = "#a86a10",
            Warn = "#c0392b",
            Border = "#c9c1b0",
            Line = "#e6e0d4",
            HeadLine = "#d8d1c2",
            Foot = "#ede8dd",
            Box = "#8a8272",
            Hover = "#10000000",
            Need = new Dictionary<string, string> { ["Feed"] = "#2b7a35", ["Drink"] = "#1d64a6", ["Rest"] = "#875f1c" },
        };

        public static Theme Get(string name) => name == "light" ? Light : Dark;

        public static SolidColorBrush Brush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        /// <summary>Blend hex color a toward b by amount (0..1).</summary>
        public static string Mix(string a, string b, double amount)
        {
            var ca = (Color)ColorConverter.ConvertFromString(a);
            var cb = (Color)ColorConverter.ConvertFromString(b);
            byte M(byte x, byte y) => (byte)Math.Round(x + (y - x) * amount);
            return $"#{M(ca.R, cb.R):x2}{M(ca.G, cb.G):x2}{M(ca.B, cb.B):x2}";
        }

        public string NeedColor(string need) =>
            need != null && Need.TryGetValue(need, out var c) ? c : (need == null ? Dim2 : Accent);

        public void Apply(ResourceDictionary res, int textSize)
        {
            res["BgBrush"] = Brush(Bg);
            res["Bg2Brush"] = Brush(Bg2);
            res["FgBrush"] = Brush(Fg);
            res["DimBrush"] = Brush(Dim);
            res["Dim2Brush"] = Brush(Dim2);
            res["AccentBrush"] = Brush(Accent);
            res["WarnBrush"] = Brush(Warn);
            res["BorderBrushC"] = Brush(Border);
            res["LineBrush"] = Brush(Line);
            res["HeadLineBrush"] = Brush(HeadLine);
            res["FootBrush"] = Brush(Foot);
            res["BoxBrush"] = Brush(Box);
            res["HoverBrush"] = Brush(Hover);
            res["NextTimesBrush"] = Brush(Mix(Dim, Bg, 0.15));

            double s = textSize;
            res["Fs"] = s;
            res["FsSm"] = Math.Max(Settings.TextMin - 1, Math.Round(s * 0.92));
            res["FsXs"] = Math.Max(Settings.TextMin - 2, Math.Round(s * 0.85));
            res["FsTitle"] = Math.Round(s * 1.3);
            res["FsMap"] = Math.Round(s * 2.25);   // map name above the table
            res["FsTime"] = Math.Round(s * 1.6);   // in-game time next to the map name
            res["FsEdit"] = s + 5;
            res["ColW"] = new GridLength(Math.Round(118 * s / 13));
        }
    }
}