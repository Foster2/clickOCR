using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ClickOCR
{
    internal static class ScreenCapture
    {
        /// <summary>Grabs a rectangle of the desktop (physical pixels) into a bitmap.</summary>
        internal static Bitmap Capture(Rectangle area)
        {
            if (area.Width <= 0 || area.Height <= 0)
                throw new ArgumentException("Empty capture area.");

            Bitmap bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
    }

    internal static class Geometry
    {
        /// <summary>
        /// Region around the click point, biased so the pointer sits at
        /// <paramref name="anchorX"/> of the width (text normally continues to the right).
        /// </summary>
        internal static Rectangle RegionAround(Rectangle available, Point point, int width, int height, double anchorX)
        {
            int w = Math.Min(width, available.Width);
            int h = Math.Min(height, available.Height);

            int x = (int)Math.Round(point.X - w * anchorX);
            int y = (int)Math.Round(point.Y - h / 2.0);

            if (x < available.Left) x = available.Left;
            if (y < available.Top) y = available.Top;
            if (x + w > available.Right) x = available.Right - w;
            if (y + h > available.Bottom) y = available.Bottom - h;
            if (x < available.Left) x = available.Left;
            if (y < available.Top) y = available.Top;

            return new Rectangle(x, y, w, h);
        }
    }
}
