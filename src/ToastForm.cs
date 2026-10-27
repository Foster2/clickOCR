using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickOCR
{
    /// <summary>Small non-activating confirmation bubble near the click point.</summary>
    internal sealed class ToastForm : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly Font _titleFont;
        private readonly Font _bodyFont;
        private readonly Rectangle _titleRect;
        private readonly Rectangle _bodyRect;
        private readonly Rectangle _accentRect;
        private readonly Timer _timer;
        private readonly Color _accent;
        private readonly string _title;
        private readonly string _body;

        protected override bool ShowWithoutActivation
        {
            // A clipboard confirmation must never steal focus from the target app.
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        internal ToastForm(string title, string body, Point anchor, bool success)
        {
            _title = title ?? "";
            _body = body ?? "";
            _accent = success ? Color.FromArgb(76, 194, 255) : Color.FromArgb(255, 176, 74);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(26, 26, 32);
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);

            float scale = 1f;
            using (Graphics probe = Graphics.FromHwnd(IntPtr.Zero))
                scale = probe.DpiX / 96f;
            if (scale <= 0f) scale = 1f;

            _titleFont = Ui.PxFont("Segoe UI", 15f * scale, FontStyle.Bold);
            _bodyFont = Ui.PxFont("Segoe UI", 14f * scale, FontStyle.Regular);

            int pad = (int)(16 * scale);
            int accent = (int)(5 * scale);
            int maxBodyWidth = (int)(430 * scale);

            Size titleSize = TextRenderer.MeasureText(_title, _titleFont, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            Size bodySize = TextRenderer.MeasureText(_body, _bodyFont, new Size(maxBodyWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

            int width = Math.Max(titleSize.Width, bodySize.Width) + pad * 2 + accent;
            int height = pad * 2 + titleSize.Height + (int)(5 * scale) + Math.Max(1, bodySize.Height);

            ClientSize = new Size(width, height);

            _accentRect = new Rectangle(0, 0, accent, height);
            _titleRect = new Rectangle(pad + accent, pad, titleSize.Width, titleSize.Height);
            _bodyRect = new Rectangle(pad + accent, pad + titleSize.Height + (int)(5 * scale),
                                      bodySize.Width, Math.Max(1, bodySize.Height));

            Ui.ApplyRoundedRegion(this, (int)(12 * scale));

            Rectangle work = Screen.FromPoint(anchor).WorkingArea;
            int x = anchor.X + (int)(18 * scale);
            int y = anchor.Y + (int)(20 * scale);
            if (x + width > work.Right) x = anchor.X - width - (int)(18 * scale);
            if (y + height > work.Bottom) y = anchor.Y - height - (int)(20 * scale);
            if (x < work.Left) x = work.Left;
            if (y < work.Top) y = work.Top;
            Location = new Point(x, y);

            _timer = new Timer();
            _timer.Interval = 2400;
            _timer.Tick += delegate { Close(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            using (SolidBrush accent = new SolidBrush(_accent))
                g.FillRectangle(accent, _accentRect);

            TextRenderer.DrawText(g, _title, _titleFont, _titleRect, Color.White,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, _body, _bodyFont, _bodyRect, Color.FromArgb(214, 219, 228),
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null) _timer.Dispose();
                if (_titleFont != null) _titleFont.Dispose();
                if (_bodyFont != null) _bodyFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
