using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.Media.Ocr;

namespace ClickOCR
{
    /// <summary>Tray application: hotkey -> freeze screen -> click -> OCR -> clipboard.</summary>
    internal sealed class App : ApplicationContext
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "ClickOCR";

        private readonly Config _config;
        private readonly NotifyIcon _tray;
        private readonly HotkeyForm _hotkey;
        private readonly bool _silent;

        private OcrService _ocr;
        private Bitmap _frame;
        private Rectangle _frameBounds;
        private OverlayForm _overlay;
        private ToastForm _toast;
        private ControlServer _control;
        private bool _busy;

        internal App(bool silent)
        {
            _silent = silent;
            _config = Config.Load();

            _hotkey = new HotkeyForm();
            _hotkey.HotkeyPressed += delegate { OnHotkey(); };

            _tray = new NotifyIcon();
            _tray.Icon = Ui.LoadAppIcon();
            _tray.Text = TrayText("ClickOCR - " + _config.Hotkey);
            _tray.Visible = true;
            _tray.DoubleClick += delegate { OnHotkey(); };
            _tray.ContextMenuStrip = BuildMenu();

            RegisterHotkey();

            _control = new ControlServer(_hotkey, HandleControlCommand);
            _control.Start();

            Logger.Write("ClickOCR started. Hotkey=" + _config.Hotkey +
                         " region=" + _config.RegionWidth + "x" + _config.RegionHeight +
                         " anchor=" + _config.ClickAnchor);

            if (!_silent)
            {
                _tray.ShowBalloonTip(2500, "ClickOCR is running",
                    "Press " + _config.Hotkey + ", then click any text on screen to copy it.",
                    ToolTipIcon.Info);
            }
        }

        // ---------------------------------------------------------------- menu

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem grab = new ToolStripMenuItem("Grab text   (" + _config.Hotkey + ")");
            grab.Font = new Font(grab.Font, FontStyle.Bold);
            grab.Click += delegate { OnHotkey(); };
            menu.Items.Add(grab);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { OpenFile(_config.FilePath); };
            menu.Items.Add(settings);

            ToolStripMenuItem startup = new ToolStripMenuItem("Start with Windows");
            startup.CheckOnClick = true;
            startup.Checked = IsStartupEnabled();
            startup.Click += delegate(object sender, EventArgs e)
            {
                ToolStripMenuItem item = (ToolStripMenuItem)sender;
                SetStartup(item.Checked);
            };
            menu.Items.Add(startup);

            ToolStripMenuItem selftest = new ToolStripMenuItem("Run self-test...");
            selftest.Click += delegate { RunSelfTest(); };
            menu.Items.Add(selftest);

            ToolStripMenuItem log = new ToolStripMenuItem("Open log");
            log.Click += delegate { OpenFile(Logger.FilePath); };
            menu.Items.Add(log);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem about = new ToolStripMenuItem("Help / About");
            about.Click += delegate { ShowAbout(); };
            menu.Items.Add(about);

            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { Quit(); };
            menu.Items.Add(exit);

            return menu;
        }

        private void ShowAbout()
        {
            string language = "not initialised yet";
            try
            {
                if (_ocr != null) language = _ocr.LanguageName;
                else language = new OcrService(_config.Language).LanguageName;
            }
            catch (Exception ex)
            {
                language = "unavailable (" + ex.Message + ")";
            }

            string text =
                "ClickOCR \u2014 click a word on screen, get the text.\r\n\r\n" +
                "1. Press " + _config.Hotkey + " (or double-click the tray icon).\r\n" +
                "2. The screen freezes and dims. Click the text you want:\r\n" +
                "      click              \u2192 the whole line / string\r\n" +
                "      Ctrl + click       \u2192 just the word\r\n" +
                "      Shift + click      \u2192 every line in the captured area\r\n" +
                "      Esc / right-click  \u2192 cancel\r\n" +
                "3. The text is on the clipboard \u2014 paste it anywhere.\r\n\r\n" +
                "OCR engine: Windows.Media.Ocr (offline, built into Windows)\r\n" +
                "Language:   " + language + "\r\n" +
                "Region:     " + _config.RegionWidth + " x " + _config.RegionHeight + " px around the click\r\n" +
                "Clickless:  " + (_config.ClicklessGrab ? "on (hotkey grabs under the pointer)" : "off") + "\r\n" +
                "Settings:   " + _config.FilePath;

            MessageBox.Show(text, "ClickOCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // --------------------------------------------------------------- grab

        private void OnHotkey()
        {
            if (_config.ClicklessGrab) ImmediateGrab();
            else BeginGrab();
        }

        /// <summary>Grabs the text under the pointer without showing the picker.</summary>
        private void ImmediateGrab()
        {
            if (_busy || _overlay != null) return;
            _busy = true;

            Rectangle bounds = SystemInformation.VirtualScreen;
            Bitmap frame = null;
            try
            {
                frame = ScreenCapture.Capture(bounds);
                Point cursor = Cursor.Position;
                Logger.Write("Clickless grab at " + cursor);
                ProcessPick(frame, bounds, cursor, GrabMode.Line);
            }
            catch (Exception ex)
            {
                if (frame != null) frame.Dispose();
                _busy = false;
                Logger.Write("Clickless grab failed: " + ex);
                ShowToast("Cannot capture the screen", ex.Message, Cursor.Position, false);
            }
        }

        private void BeginGrab()
        {
            if (_busy || _overlay != null) return;
            _busy = true;

            try
            {
                // A leftover confirmation bubble must not end up in the next capture.
                if (_toast != null)
                {
                    try { _toast.Close(); } catch { }
                    _toast = null;
                }

                _frameBounds = SystemInformation.VirtualScreen;
                _frame = ScreenCapture.Capture(_frameBounds);
                Logger.Write("Grab started (virtual screen " + _frameBounds + ")");

                OverlayForm overlay = new OverlayForm(_frame, _frameBounds, _config);
                overlay.Picked += OnPicked;
                overlay.Cancelled += OnCancelled;
                _overlay = overlay;

                overlay.Show();
                overlay.Activate();
                overlay.BringToFront();
            }
            catch (Exception ex)
            {
                Logger.Write("Grab start failed: " + ex);
                CleanupOverlay();
                if (_frame != null) { _frame.Dispose(); _frame = null; }
                _busy = false;
                ShowToast("Cannot capture the screen", ex.Message, Cursor.Position, false);
            }
        }

        private void OnCancelled()
        {
            CleanupOverlay();
            if (_frame != null) { _frame.Dispose(); _frame = null; }
            _busy = false;
        }

        private void OnPicked(Point screenPoint, GrabMode mode)
        {
            Bitmap frame = _frame;
            Rectangle bounds = _frameBounds;
            Logger.Write("Pick at " + screenPoint + ", mode=" + mode);
            CleanupOverlay();

            if (frame == null)
            {
                _busy = false;
                return;
            }

            ProcessPick(frame, bounds, screenPoint, mode);
        }

        /// <summary>Crops the frozen frame around the click, OCRs it and copies the hit text.</summary>
        private void ProcessPick(Bitmap frame, Rectangle bounds, Point screenPoint, GrabMode mode)
        {
            Bitmap crop = null;
            Point cropPoint = Point.Empty;

            try
            {
                Point local = new Point(screenPoint.X - bounds.X, screenPoint.Y - bounds.Y);
                Rectangle region = Geometry.RegionAround(new Rectangle(Point.Empty, frame.Size), local,
                    _config.RegionWidth, _config.RegionHeight, _config.ClickAnchor);

                crop = frame.Clone(region, PixelFormat.Format32bppPArgb);
                cropPoint = new Point(local.X - region.X, local.Y - region.Y);
            }
            catch (Exception ex)
            {
                Logger.Write("Crop failed: " + ex);
            }
            finally
            {
                frame.Dispose();
                if (ReferenceEquals(_frame, frame)) _frame = null;
            }

            if (crop == null)
            {
                _busy = false;
                ShowToast("Cannot read that spot", "The capture could not be cropped.", screenPoint, false);
                return;
            }

            RunOcr(crop, cropPoint, mode, screenPoint);
        }

        private async void RunOcr(Bitmap crop, Point cropPoint, GrabMode mode, Point screenPoint)
        {
            string text = null;
            string error = null;
            int width = crop.Width;
            int height = crop.Height;
            Stopwatch watch = Stopwatch.StartNew();

            try
            {
                OcrService ocr = EnsureOcr();
                PickResult picked = await System.Threading.Tasks.Task.Run<PickResult>(delegate
                {
                    OcrResult result = ocr.Recognize(crop);
                    return TextPicker.Pick(result, cropPoint, mode);
                });

                text = picked.Text;
                Logger.Write(string.Format("OCR ok in {0} ms ({1}x{2}, mode={3}, word='{4}', distance={5:0.0}) -> '{6}'",
                    watch.ElapsedMilliseconds, width, height, mode, picked.MatchedWord, picked.Distance,
                    Ui.Shorten(text, 160)));
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Write("OCR failed: " + ex);
            }
            finally
            {
                crop.Dispose();
            }

            _busy = false;

            if (error != null)
            {
                ShowToast("OCR failed", Ui.Shorten(error, 200), screenPoint, false);
                return;
            }

            text = (text ?? "").Trim();
            if (text.Length == 0)
            {
                ShowToast("No text found", "Nothing readable near that spot. Try the word itself.", screenPoint, false);
                return;
            }

            bool copied = Ui.TryCopyText(text);
            ShowToast(copied ? "Copied to clipboard" : "Clipboard is busy",
                      Ui.Shorten(text, 150), screenPoint, copied);
        }

        private OcrService EnsureOcr()
        {
            if (_ocr == null) _ocr = new OcrService(_config.Language);
            return _ocr;
        }

        private void CleanupOverlay()
        {
            OverlayForm overlay = _overlay;
            _overlay = null;
            if (overlay == null) return;

            overlay.Picked -= OnPicked;
            overlay.Cancelled -= OnCancelled;
            try { overlay.Hide(); } catch { }

            // A pick arrives from inside the overlay's own MouseDown handler, so the
            // form must not be destroyed while that message is still being processed.
            try
            {
                _hotkey.BeginInvoke((MethodInvoker)delegate
                {
                    try { overlay.Dispose(); } catch { }
                });
            }
            catch (Exception)
            {
                try { overlay.Dispose(); } catch { }
            }
        }

        private void ShowToast(string title, string body, Point anchor, bool success)
        {
            try
            {
                if (_toast != null)
                {
                    try { _toast.Close(); } catch { }
                    _toast = null;
                }
                _toast = new ToastForm(title, body, anchor, success);
                _toast.Show();
            }
            catch (Exception ex)
            {
                Logger.Write("Toast failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------ control

        private string HandleControlCommand(string command)
        {
            string[] parts = command.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "ERR empty";

            switch (parts[0].ToLowerInvariant())
            {
                case "ping":
                    return "OK pong";

                case "status":
                    return "OK " + (_overlay != null ? "picking" : "idle") + " hotkey=" + _config.Hotkey +
                           " region=" + _config.RegionWidth + "x" + _config.RegionHeight;

                case "grab":
                    BeginGrab();
                    return _overlay != null ? "OK grabbing" : "ERR grab failed";

                case "grabcursor":
                    ImmediateGrab();
                    return "OK grabbing cursor";

                case "pick":
                    return ControlPick(parts);

                case "pickclient":
                    return ControlPickClient(parts);

                case "quit":
                    Quit();
                    return "OK bye";

                default:
                    return "ERR unknown command";
            }
        }

        private string ControlPick(string[] parts)
        {
            if (_overlay == null) return "ERR not picking";

            int x;
            int y;
            if (parts.Length < 3) return "ERR pick <x> <y> [line|word|block]";
            if (!int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y)) return "ERR bad coordinates";

            GrabMode mode = GrabMode.Line;
            if (parts.Length >= 4 && !TryParseMode(parts[3], out mode)) return "ERR bad mode";

            return _overlay.SimulatePick(new Point(x, y), mode) ? "OK picked" : "ERR already finished";
        }

        /// <summary>Posts a real WM_LBUTTONDOWN to the overlay so the actual mouse handler runs.</summary>
        private string ControlPickClient(string[] parts)
        {
            if (_overlay == null) return "ERR not picking";

            int x;
            int y;
            if (parts.Length < 3 || !int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y))
                return "ERR pickclient <clientX> <clientY>";

            IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
            NativeMethods.PostMessage(_overlay.Handle, NativeMethods.WM_LBUTTONDOWN, (IntPtr)1, lParam);
            return "OK click posted";
        }

        private static bool TryParseMode(string value, out GrabMode mode)
        {
            mode = GrabMode.Line;
            switch ((value ?? "").ToLowerInvariant())
            {
                case "line": mode = GrabMode.Line; return true;
                case "word": mode = GrabMode.Word; return true;
                case "block": mode = GrabMode.Block; return true;
                default: return false;
            }
        }

        // ------------------------------------------------------------- hotkey
        private void RegisterHotkey()
        {
            if (_hotkey.Register(_config.Hotkey)) return;

            string[] fallbacks = new string[] { "Ctrl+Alt+O", "Ctrl+Shift+O", "Ctrl+Alt+F9", "Ctrl+Alt+Shift+O" };
            foreach (string candidate in fallbacks)
            {
                if (string.Equals(candidate, _config.Hotkey, StringComparison.OrdinalIgnoreCase)) continue;
                if (_hotkey.Register(candidate))
                {
                    _tray.Text = TrayText("ClickOCR - " + candidate);
                    _tray.ShowBalloonTip(4000, "Hotkey changed",
                        "'" + _config.Hotkey + "' is taken by another program. ClickOCR now uses " + candidate + ".",
                        ToolTipIcon.Warning);
                    return;
                }
            }

            _tray.Text = TrayText("ClickOCR - no hotkey");
            _tray.ShowBalloonTip(5000, "No hotkey available",
                "ClickOCR could not register a global shortcut. Use the tray icon to grab text.",
                ToolTipIcon.Error);
        }

        // ------------------------------------------------------------ startup

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    return key.GetValue(RunValueName) != null;
                }
            }
            catch (Exception ex)
            {
                Logger.Write("IsStartupEnabled: " + ex.Message);
                return false;
            }
        }

        private void SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enabled)
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\" --silent");
                    else
                        key.DeleteValue(RunValueName, false);
                }
                Logger.Write("Start with Windows: " + enabled);
            }
            catch (Exception ex)
            {
                Logger.Write("SetStartup failed: " + ex.Message);
                MessageBox.Show("Could not change the autostart entry:\r\n" + ex.Message, "ClickOCR",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ----------------------------------------------------------- self test

        private void RunSelfTest()
        {
            Thread thread = new Thread(delegate ()
            {
                try { SelfTest.Run(); }
                catch (Exception ex) { Logger.Write("SelfTest crashed: " + ex); }
                OpenFile(SelfTest.ReportPath);
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void OpenFile(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                Logger.Write("Cannot open " + path + ": " + ex.Message);
            }
        }

        /// <summary>NotifyIcon.Text is limited to 63 characters.</summary>
        private static string TrayText(string text)
        {
            return text.Length > 63 ? text.Substring(0, 63) : text;
        }

        // --------------------------------------------------------------- exit

        private void Quit()
        {
            try { if (_control != null) _control.Dispose(); } catch { }
            try { _tray.Visible = false; _tray.Dispose(); } catch { }
            try { _hotkey.Unregister(); _hotkey.Dispose(); } catch { }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_control != null) _control.Dispose(); } catch { }
                try { _hotkey.Unregister(); } catch { }
                try { if (_hotkey != null) _hotkey.Dispose(); } catch { }
                try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
                try { if (_toast != null) _toast.Dispose(); } catch { }
                try { if (_frame != null) _frame.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}

