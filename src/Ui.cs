using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClickOCR
{
    internal static class Ui
    {
        internal static Font PxFont(string family, float pixels, FontStyle style)
        {
            return new Font(family, pixels, style, GraphicsUnit.Pixel);
        }

        internal static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void ApplyRoundedRegion(Form form, int radius)
        {
            IntPtr handle = NativeMethods.CreateRoundRectRgn(0, 0, form.Width + 1, form.Height + 1, radius, radius);
            if (handle == IntPtr.Zero) return;
            try
            {
                form.Region = Region.FromHrgn(handle);
            }
            finally
            {
                NativeMethods.DeleteObject(handle);
            }
        }

        internal static string Shorten(string text, int max)
        {
            if (text == null) return "";
            string single = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (single.IndexOf("  ") >= 0) single = single.Replace("  ", " ");
            if (single.Length <= max) return single;
            return single.Substring(0, Math.Max(0, max - 1)) + "\u2026";
        }

        internal static bool TryCopyText(string text)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true, 4, 40);
                    return true;
                }
                catch (ThreadStateException)
                {
                    Logger.Write("Clipboard: not on an STA thread");
                    return false;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(60);
                }
                catch (Exception ex)
                {
                    Logger.Write("Clipboard: " + ex.Message);
                    Thread.Sleep(60);
                }
            }
            Logger.Write("Clipboard stayed locked; text was not copied.");
            return false;
        }

        internal static Icon LoadAppIcon()
        {
            try
            {
                Assembly assembly = typeof(Ui).Assembly;
                string[] names = new string[] { "app.ico", "ClickOCR.app.ico" };
                foreach (string name in names)
                {
                    Stream stream = assembly.GetManifestResourceStream(name);
                    if (stream == null) continue;
                    using (stream)
                    {
                        return new Icon(stream, new Size(32, 32));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Icon load failed: " + ex.Message);
            }
            return SystemIcons.Application;
        }

        internal static float DpiScale(Control control)
        {
            try
            {
                using (Graphics g = control.CreateGraphics())
                {
                    return g.DpiX / 96f;
                }
            }
            catch
            {
                return 1f;
            }
        }
    }
}
