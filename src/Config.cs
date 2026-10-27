using System;
using System.Globalization;
using System.IO;

namespace ClickOCR
{
    /// <summary>Ini-style settings, created with defaults on first run.</summary>
    internal sealed class Config
    {
        internal const string DefaultText =
@"; ClickOCR settings -------------------------------------------------
; Restart ClickOCR after changing this file.
;
; Hotkey        global shortcut that starts a grab (Ctrl / Alt / Shift / Win + key)
; RegionWidth   how much of the screen around the click is OCR'd, in pixels
; RegionHeight  ... vertically
; ClickAnchor   where the click sits inside that region, 0 = far left, 1 = far right
;               (text usually continues to the right of the pointer, hence 0.34)
; Dim           how much the frozen screen is darkened while you pick (0 - 0.85)
; Language      OCR language tag, e.g. en-US, de-DE, zh-Hans-CN.
;               Leave empty to use your Windows display language.
; ClicklessGrab 1 = skip the picker and OCR straight under the mouse pointer
;               (point at the text, press the hotkey, done). 0 = click to pick.

Hotkey=Ctrl+Alt+O
RegionWidth=1400
RegionHeight=320
ClickAnchor=0.34
Dim=0.35
Language=
ClicklessGrab=0
";

        internal string FilePath;

        internal string Hotkey = "Ctrl+Alt+O";
        internal int RegionWidth = 1400;
        internal int RegionHeight = 320;
        internal double ClickAnchor = 0.34;
        internal double Dim = 0.35;
        internal string Language = "";
        internal bool ClicklessGrab = false;

        internal static Config Load()
        {
            Config config = new Config();
            config.FilePath = Path.Combine(Logger.Folder, "config.ini");

            if (!File.Exists(config.FilePath))
            {
                try
                {
                    Directory.CreateDirectory(Logger.Folder);
                    File.WriteAllText(config.FilePath, DefaultText);
                }
                catch (Exception ex)
                {
                    Logger.Write("Cannot create config: " + ex.Message);
                }
            }

            try
            {
                foreach (string raw in File.ReadAllLines(config.FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    config.Apply(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Cannot read config: " + ex.Message);
            }

            config.Normalize();
            return config;
        }

        private void Apply(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "hotkey":
                    if (value.Length > 0) Hotkey = value;
                    break;
                case "regionwidth":
                    int w;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out w)) RegionWidth = w;
                    break;
                case "regionheight":
                    int h;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out h)) RegionHeight = h;
                    break;
                case "clickanchor":
                    double a;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out a)) ClickAnchor = a;
                    break;
                case "dim":
                    double d;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) Dim = d;
                    break;
                case "language":
                    Language = value;
                    break;
                case "clicklessgrab":
                    ClicklessGrab = value == "1" ||
                                    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        private void Normalize()
        {
            RegionWidth = ClampInt(RegionWidth, 120, 8000);
            RegionHeight = ClampInt(RegionHeight, 60, 4000);
            if (ClickAnchor < 0.0) ClickAnchor = 0.0;
            if (ClickAnchor > 1.0) ClickAnchor = 1.0;
            if (Dim < 0.0) Dim = 0.0;
            if (Dim > 0.85) Dim = 0.85;
            if (Hotkey == null || Hotkey.Trim().Length == 0) Hotkey = "Ctrl+Alt+O";
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
