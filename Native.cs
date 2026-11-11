using System;
using System.Runtime.InteropServices;

namespace NeedZones
{
    /// <summary>Win32 calls for click-through and global hotkeys.</summary>
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static long GetExStyle(IntPtr hwnd) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64() : GetWindowLong32(hwnd, GWL_EXSTYLE);

        public static void SetExStyle(IntPtr hwnd, long style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GWL_EXSTYLE, new IntPtr(style));
            else SetWindowLong32(hwnd, GWL_EXSTYLE, (int)style);
        }

        /// <summary>"ctrl+alt+h" -> (modifiers, virtual key) or null.</summary>
        public static (uint mods, uint vk)? ParseHotkey(string text)
        {
            uint mods = 0, vk = 0;
            foreach (var raw in (text ?? "").ToLowerInvariant().Replace(" ", "").Split('+'))
            {
                if (raw == "ctrl" || raw == "control") mods |= MOD_CONTROL;
                else if (raw == "alt") mods |= MOD_ALT;
                else if (raw == "shift") mods |= MOD_SHIFT;
                else if (raw == "win") mods |= MOD_WIN;
                else if (raw.Length == 1 && char.IsLetterOrDigit(raw[0])) vk = char.ToUpperInvariant(raw[0]);
                else if (raw.Length >= 2 && raw[0] == 'f' && int.TryParse(raw.Substring(1), out int f) && f >= 1 && f <= 24)
                    vk = (uint)(0x70 + f - 1);
                else return null;
            }
            return vk == 0 ? ((uint, uint)?)null : (mods, vk);
        }

        public static string NiceHotkey(string text)
        {
            var parts = (text ?? "").Split('+');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join("+", parts);
        }
    }
}
