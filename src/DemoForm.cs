using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ClickOCR
{
    /// <summary>
    /// Sample window used to try the tool (and by the automated end-to-end test):
    /// a fake invoice plus a live clipboard panel, so a click's result is visible
    /// without pasting anywhere.
    /// It writes the on-screen coordinates of a few known words to
    /// %APPDATA%\ClickOCR\demo-anchors.txt so a script can click them exactly.
    /// </summary>
    internal sealed class DemoForm : Form
    {
        private readonly Font _sampleFont;
        private readonly Font _hintFont;
        private readonly Font _headFont;
        private readonly Font _bodyFont;
        private readonly Font _footFont;
        private readonly Timer _autoClose;
        private readonly Timer _clipboard;

        private string _clipText = "";
        private DateTime _clipChangedAt = DateTime.MinValue;

        // invoice card inside the stage window
        private const int CardX = 130;
        private const int CardY = 196;
        private const int CardWidth = 900;
        private const int CardHeight = 240;

        private const int PanelY = 448;
        private const int PanelHeight = 196;

        private const int MarginX = 32;
        private const int Line1Y = 46;
        private const int Line2Y = 104;
        private const int Line3Y = 162;

        private const string Line1 = "Invoice INV-2026-0042 for Acme Corp";
        private const string Line2 = "Total: 1,299.00 EUR (due 2026-11-15)";
        private const string Line3 = "reference: 9f2c-77b1-alpha";

        internal DemoForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(1160, 660);
            BackColor = Color.FromArgb(16, 16, 20);
            ShowInTaskbar = true;
            TopMost = true;                 // stay visible above other desktop windows
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);

            _sampleFont = Ui.PxFont("Segoe UI", 30f, FontStyle.Regular);
            _hintFont = Ui.PxFont("Segoe UI", 15f, FontStyle.Regular);
            _headFont = Ui.PxFont("Segoe UI", 12f, FontStyle.Bold);
            _bodyFont = Ui.PxFont("Segoe UI", 17f, FontStyle.Regular);
            _footFont = Ui.PxFont("Segoe UI", 12f, FontStyle.Regular);

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Left + Math.Max(0, (work.Width - Width) / 2),
                                 work.Top + Math.Max(0, (work.Height - Height) / 2));

            _autoClose = new Timer();
            _autoClose.Interval = 180000;
            _autoClose.Tick += delegate { Close(); };

            _clipboard = new Timer();
            _clipboard.Interval = 150;
            _clipboard.Tick += delegate
            {
                string current = ReadClipboard();
                if (current != _clipText)
                {
                    _clipText = current;
                    _clipChangedAt = DateTime.Now;
                }
                Invalidate(new Rectangle(0, PanelY - 8, Width, PanelHeight + 24));
            };
        }

        private static string ReadClipboard()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _autoClose.Start();
            _clipboard.Start();
            _clipText = ReadClipboard();
            WriteAnchors();
            Activate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle,
                       Color.FromArgb(23, 25, 33), Color.FromArgb(10, 10, 14), 90f))
                g.FillRectangle(brush, ClientRectangle);

            // invoice card
            g.FillRectangle(Brushes.White, CardX, CardY, CardWidth, CardHeight);
            TextRenderer.DrawText(g, Line1, _sampleFont, new Point(CardX + MarginX, CardY + Line1Y),
                Color.FromArgb(16, 16, 16), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Line2, _sampleFont, new Point(CardX + MarginX, CardY + Line2Y),
                Color.FromArgb(16, 16, 16), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Line3, _sampleFont, new Point(CardX + MarginX, CardY + Line3Y),
                Color.FromArgb(16, 16, 16), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, "ClickOCR demo - press Ctrl+Alt+O, then click a line above. Esc closes this window.",
                _hintFont, new Point(CardX + MarginX, CardY + 208), Color.FromArgb(120, 120, 120),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            DrawClipboardPanel(g);
        }

        private void DrawClipboardPanel(Graphics g)
        {
            bool flash = (DateTime.Now - _clipChangedAt).TotalMilliseconds < 700;
            Color accent = flash ? Color.FromArgb(120, 220, 255) : Color.FromArgb(60, 190, 245);

            using (SolidBrush background = new SolidBrush(Color.FromArgb(24, 24, 30)))
                g.FillRectangle(background, CardX, PanelY, CardWidth, PanelHeight);

            using (Pen border = new Pen(Color.FromArgb(flash ? 200 : 90, accent)))
                g.DrawRectangle(border, CardX, PanelY, CardWidth - 1, PanelHeight - 1);

            using (SolidBrush bar = new SolidBrush(Color.FromArgb(flash ? 255 : 130, accent)))
                g.FillRectangle(bar, CardX, PanelY, 5, PanelHeight);

            using (SolidBrush dot = new SolidBrush(accent))
                g.FillEllipse(dot, CardX + 30, PanelY + 26, 10, 10);

            TextRenderer.DrawText(g, "CLIPBOARD", _headFont, new Point(CardX + 50, PanelY + 22),
                Color.FromArgb(190, 198, 214), TextFormatFlags.NoPadding);

            string stamp = _clipChangedAt == DateTime.MinValue ? "watching" : _clipChangedAt.ToString("HH:mm:ss");
            Size stampSize = TextRenderer.MeasureText(stamp, _footFont, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, stamp, _footFont,
                new Point(CardX + CardWidth - 28 - stampSize.Width, PanelY + 24),
                Color.FromArgb(120, 130, 148), TextFormatFlags.NoPadding);

            Rectangle body = new Rectangle(CardX + 30, PanelY + 60, CardWidth - 60, PanelHeight - 96);
            if (_clipText.Length == 0)
            {
                TextRenderer.DrawText(g, "(empty)", _bodyFont, body, Color.FromArgb(90, 98, 115),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
            else
            {
                TextRenderer.DrawText(g, _clipText.TrimEnd(), _bodyFont, body,
                    flash ? Color.White : Color.FromArgb(226, 232, 242),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void WriteAnchors()
        {
            try
            {
                int lineHeight = TextRenderer.MeasureText("Xg", _sampleFont).Height;

                int invoiceWidth = TextRenderer.MeasureText("Invoice", _sampleFont,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                int prefixAcme = TextRenderer.MeasureText("Invoice INV-2026-0042 for ", _sampleFont,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                int acmeWidth = TextRenderer.MeasureText("Acme", _sampleFont,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                int totalPrefix = TextRenderer.MeasureText("Total: ", _sampleFont,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
                int totalWidth = TextRenderer.MeasureText("Total: 1,299.00", _sampleFont,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;

                Point invoice = PointToScreen(new Point(CardX + MarginX + invoiceWidth / 2, CardY + Line1Y + lineHeight / 2));
                Point acme = PointToScreen(new Point(CardX + MarginX + prefixAcme + acmeWidth / 2, CardY + Line1Y + lineHeight / 2));
                Point total = PointToScreen(new Point(CardX + MarginX + (totalPrefix + totalWidth) / 2, CardY + Line2Y + lineHeight / 2));
                Point reference = PointToScreen(new Point(CardX + MarginX + 40, CardY + Line3Y + lineHeight / 2));

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("window=" + Left.ToString(CultureInfo.InvariantCulture) + "," + Top.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("size=" + Width.ToString(CultureInfo.InvariantCulture) + "x" + Height.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("invoice=" + invoice.X + "," + invoice.Y);
                builder.AppendLine("acme=" + acme.X + "," + acme.Y);
                builder.AppendLine("total=" + total.X + "," + total.Y);
                builder.AppendLine("reference=" + reference.X + "," + reference.Y);

                Directory.CreateDirectory(Logger.Folder);
                File.WriteAllText(SelfTest.AnchorsPath, builder.ToString());
            }
            catch (Exception ex)
            {
                Logger.Write("Demo anchors failed: " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_autoClose != null) _autoClose.Dispose();
                if (_clipboard != null) _clipboard.Dispose();
                if (_sampleFont != null) _sampleFont.Dispose();
                if (_hintFont != null) _hintFont.Dispose();
                if (_headFont != null) _headFont.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
                if (_footFont != null) _footFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
