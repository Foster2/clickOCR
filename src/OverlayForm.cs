using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickOCR
{
    /// <summary>
    /// Full-desktop picker: shows the frozen screen, dims it, and turns the next
    /// click into a screen point plus a grab mode.
    /// </summary>
    internal sealed class OverlayForm : Form
    {
        private readonly Bitmap _frame;
        private readonly Rectangle _screenBounds;
        private readonly Config _config;

        private bool _finished;
        private float _scale = 1f;
        private Timer _timeout;

        internal event Action<Point, GrabMode> Picked;
        internal event Action Cancelled;

        internal OverlayForm(Bitmap frame, Rectangle screenBounds, Config config)
        {
            _frame = frame;
            _screenBounds = screenBounds;
            _config = config;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenBounds;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = Ui.DpiScale(this);

            // Safety net: never leave the desktop dimmed and swallowing clicks forever.
            _timeout = new Timer();
            _timeout.Interval = 45000;
            _timeout.Tick += delegate { Cancel(); };
            _timeout.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timeout != null)
            {
                _timeout.Dispose();
                _timeout = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(_frame, 0, 0);

            int alpha = (int)Math.Round(255 * _config.Dim);
            if (alpha > 0)
            {
                using (SolidBrush dim = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    g.FillRectangle(dim, ClientRectangle);
            }

            DrawHint(g);
        }

        private void DrawHint(Graphics g)
        {
            string text = "Click the text to copy     \u00b7     Ctrl+click = word     \u00b7     Shift+click = all text     \u00b7     Esc = cancel";

            Rectangle monitor = Screen.FromPoint(Cursor.Position).Bounds;
            Rectangle local = new Rectangle(monitor.X - _screenBounds.X, monitor.Y - _screenBounds.Y,
                                            monitor.Width, monitor.Height);

            using (Font font = Ui.PxFont("Segoe UI", 15f * _scale, FontStyle.Bold))
            {
                Size size = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

                int padX = (int)(20 * _scale);
                int padY = (int)(11 * _scale);
                Rectangle box = new Rectangle(
                    local.X + (local.Width - size.Width) / 2 - padX,
                    local.Y + (int)(26 * _scale) - padY,
                    size.Width + padX * 2,
                    size.Height + padY * 2);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Ui.Rounded(box, (int)(10 * _scale)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(235, 18, 18, 22)))
                using (Pen border = new Pen(Color.FromArgb(90, 255, 255, 255)))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }

                TextRenderer.DrawText(g, text, font, box, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Right)
            {
                Cancel();
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            GrabMode mode = GrabMode.Line;
            Keys modifiers = Control.ModifierKeys;
            if ((modifiers & Keys.Control) == Keys.Control) mode = GrabMode.Word;
            else if ((modifiers & Keys.Shift) == Keys.Shift) mode = GrabMode.Block;

            Point screenPoint = new Point(_screenBounds.Left + e.X, _screenBounds.Top + e.Y);
            SimulatePick(screenPoint, mode);
        }

        /// <summary>
        /// Same code path as a real mouse click, but with the point supplied by the
        /// caller (used by the control pipe / automation).
        /// </summary>
        internal bool SimulatePick(Point screenPoint, GrabMode mode)
        {
            if (!Finish()) return false;
            Action<Point, GrabMode> handler = Picked;
            if (handler != null) handler(screenPoint, mode);
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Cancel();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Cancel();
        }

        private void Cancel()
        {
            if (!Finish()) return;
            Action handler = Cancelled;
            if (handler != null) handler();
        }

        /// <summary>Hides and closes the overlay exactly once.</summary>
        private bool Finish()
        {
            if (_finished) return false;
            _finished = true;
            Hide();
            Close();
            return true;
        }
    }
}
