using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ClickOCR
{
    /// <summary>
    /// Sample window used to try the tool (and by the automated end-to-end test).
    /// It writes the on-screen coordinates of a few known words to
    /// %APPDATA%\ClickOCR\demo-anchors.txt so a script can click them exactly.
    /// </summary>
    internal sealed class DemoForm : Form
    {
        private readonly Font _sampleFont;
        private readonly Font _hintFont;
        private readonly Timer _autoClose;

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
            ClientSize = new Size(900, 240);
            BackColor = Color.White;
            ShowInTaskbar = true;
            TopMost = true;                 // stay visible above other desktop windows
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);

            _sampleFont = Ui.PxFont("Segoe UI", 30f, FontStyle.Regular);
            _hintFont = Ui.PxFont("Segoe UI", 15f, FontStyle.Regular);

            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Left + Math.Max(0, (work.Width - Width) / 2),
                                 work.Top + Math.Max(0, (work.Height - Height) / 2));

            _autoClose = new Timer();
            _autoClose.Interval = 180000;
            _autoClose.Tick += delegate { Close(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _autoClose.Start();
            WriteAnchors();
            Activate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            TextRenderer.DrawText(g, Line1, _sampleFont, new Point(MarginX, Line1Y), Color.FromArgb(16, 16, 16),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Line2, _sampleFont, new Point(MarginX, Line2Y), Color.FromArgb(16, 16, 16),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, Line3, _sampleFont, new Point(MarginX, Line3Y), Color.FromArgb(16, 16, 16),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, "ClickOCR demo - press " + "Ctrl+Alt+O" + ", then click a word above. Esc closes this window.",
                _hintFont, new Point(MarginX, 208), Color.FromArgb(120, 120, 120),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
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

                Point invoice = PointToScreen(new Point(MarginX + invoiceWidth / 2, Line1Y + lineHeight / 2));
                Point acme = PointToScreen(new Point(MarginX + prefixAcme + acmeWidth / 2, Line1Y + lineHeight / 2));
                Point total = PointToScreen(new Point(MarginX + (totalPrefix + totalWidth) / 2, Line2Y + lineHeight / 2));
                Point reference = PointToScreen(new Point(MarginX + 40, Line3Y + lineHeight / 2));

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("window=" + Left.ToString(CultureInfo.InvariantCulture) + "," + Top.ToString(CultureInfo.InvariantCulture));
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
                if (_sampleFont != null) _sampleFont.Dispose();
                if (_hintFont != null) _hintFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
