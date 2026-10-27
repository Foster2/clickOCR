using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClickOCR
{
    internal static class NativeMethods
    {
        internal const int WM_HOTKEY = 0x0312;

        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_WIN = 0x0008;
        internal const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        internal const int WM_LBUTTONDOWN = 0x0201;

        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("shcore.dll")]
        internal static extern int SetProcessDpiAwareness(int value);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr obj);

        /// <summary>
        /// Use per-monitor DPI awareness so that screen coordinates, click points and
        /// OCR bounding boxes all live in the same (physical pixel) space.
        /// </summary>
        internal static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch (Exception) { }

            try
            {
                // PROCESS_PER_MONITOR_DPI_AWARE
                if (SetProcessDpiAwareness(2) == 0) return;
            }
            catch (Exception) { }

            try { SetProcessDPIAware(); }
            catch (Exception) { }
        }

        internal static bool TryParseHotkey(string spec, out uint modifiers, out Keys key)
        {
            modifiers = 0;
            key = Keys.None;
            if (string.IsNullOrEmpty(spec)) return false;

            string[] parts = spec.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) continue;

                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "alt":
                        modifiers |= MOD_ALT;
                        break;
                    case "shift":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= MOD_WIN;
                        break;
                    default:
                        Keys parsed;
                        if (part.Length == 1 && char.IsDigit(part[0]))
                        {
                            parsed = (Keys)Enum.Parse(typeof(Keys), "D" + part);
                        }
                        else if (!Enum.TryParse<Keys>(part, true, out parsed))
                        {
                            return false;
                        }
                        key = parsed;
                        break;
                }
            }

            return modifiers != 0 && key != Keys.None;
        }
    }
}
