using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Windows.Media.Ocr;

namespace ClickOCR
{
    /// <summary>
    /// Diagnostics: verifies the OCR engine, the hit-testing and the clipboard.
    /// Writes a plain-text report so it can be run headless.
    /// </summary>
    internal static class SelfTest
    {
        internal static string ReportPath
        {
            get { return Path.Combine(Logger.Folder, "selftest.txt"); }
        }

        internal static string AnchorsPath
        {
            get { return Path.Combine(Logger.Folder, "demo-anchors.txt"); }
        }

        internal static int Run()
        {
            StringBuilder report = new StringBuilder();
            bool ok = true;

            report.AppendLine("ClickOCR self-test");
            report.AppendLine("==================");
            report.AppendLine("time      : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("OS        : " + Environment.OSVersion);
            report.AppendLine("exe       : " + Application.ExecutablePath);
            report.AppendLine("settings  : " + Path.Combine(Logger.Folder, "config.ini"));
            report.AppendLine();

            // 1. OCR languages
            report.AppendLine("[1] OCR languages");
            try
            {
                IReadOnlyList<Windows.Globalization.Language> languages = OcrEngine.AvailableRecognizerLanguages;
                foreach (Windows.Globalization.Language language in languages)
                    report.AppendLine("    " + language.LanguageTag + "  " + language.DisplayName);
                if (languages.Count == 0)
                {
                    report.AppendLine("    NONE INSTALLED");
                    ok = false;
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("    FAILED: " + ex.Message);
                ok = false;
            }
            report.AppendLine();

            Config config = Config.Load();

            // 2. Off-screen render -> OCR -> hit test
            report.AppendLine("[2] Synthetic text (off-screen)");
            OcrService service = null;
            Point invoicePoint;
            Point wordPoint;
            try
            {
                service = new OcrService(config.Language);
                report.AppendLine("    engine language: " + service.LanguageName);

                using (Bitmap bitmap = RenderSample(out invoicePoint, out wordPoint))
                {
                    OcrResult result = service.Recognize(bitmap);
                    report.AppendLine("    text: " + Ui.Shorten(result.Text, 200));

                    PickResult line = TextPicker.Pick(result, invoicePoint, GrabMode.Line);
                    PickResult word = TextPicker.Pick(result, wordPoint, GrabMode.Word);

                    report.AppendLine("    click@(" + invoicePoint.X + "," + invoicePoint.Y + ") line -> " + line.Text);
                    report.AppendLine("    click@(" + wordPoint.X + "," + wordPoint.Y + ") word -> " + word.Text);

                    bool lineOk = line.Text.IndexOf("INV-2026-0042", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool wordOk = string.Equals(word.Text, "Acme", StringComparison.OrdinalIgnoreCase);
                    report.AppendLine("    line hit : " + (lineOk ? "PASS" : "FAIL"));
                    report.AppendLine("    word hit : " + (wordOk ? "PASS" : "FAIL"));
                    if (!lineOk || !wordOk) ok = false;
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("    FAILED: " + ex);
                ok = false;
            }
            report.AppendLine();

            // 3. Real screen capture
            report.AppendLine("[3] Screen capture + OCR");
            try
            {
                Rectangle area = SystemInformation.VirtualScreen;
                report.AppendLine("    virtual screen: " + area);
                using (Bitmap screen = ScreenCapture.Capture(area))
                {
                    OcrResult result = service.Recognize(screen);
                    int lines = 0;
                    foreach (OcrLine line in result.Lines) lines++;
                    report.AppendLine("    captured: " + screen.Width + "x" + screen.Height + ", lines: " + lines);
                    report.AppendLine("    preview: " + Ui.Shorten(result.Text, 300));
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("    FAILED: " + ex.Message);
                ok = false;
            }
            report.AppendLine();

            // 4. Clipboard round-trip
            report.AppendLine("[4] Clipboard");
            string marker = "ClickOCR clipboard test " + Guid.NewGuid().ToString("N").Substring(0, 8);
            try
            {
                bool copied = Ui.TryCopyText(marker);
                string back = copied ? Clipboard.GetText() : "";
                report.AppendLine("    wrote: " + marker);
                report.AppendLine("    read : " + back);
                bool pass = copied && string.Equals(back, marker, StringComparison.Ordinal);
                report.AppendLine("    " + (pass ? "PASS" : "FAIL"));
                if (!pass) ok = false;
            }
            catch (Exception ex)
            {
                report.AppendLine("    FAILED: " + ex.Message);
                ok = false;
            }

            report.AppendLine();
            report.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");

            try
            {
                Directory.CreateDirectory(Logger.Folder);
                File.WriteAllText(ReportPath, report.ToString());
            }
            catch (Exception ex)
            {
                Logger.Write("Cannot write self-test report: " + ex.Message);
            }

            Logger.Write("Self-test " + (ok ? "PASS" : "FAIL"));
            return ok ? 0 : 1;
        }

        private static Bitmap RenderSample(out Point linePoint, out Point wordPoint)
        {
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            Bitmap bitmap = new Bitmap(900, 200, PixelFormat.Format32bppArgb);
            linePoint = Point.Empty;
            wordPoint = Point.Empty;

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                using (Font font = Ui.PxFont("Segoe UI", 30f, FontStyle.Regular))
                {
                    // Words are drawn one by one so the click anchors are exact.
                    string[] words = new string[] { "Invoice", "INV-2026-0042", "for", "Acme", "Corp" };
                    int x = 20;
                    int y = 40;
                    int height = TextRenderer.MeasureText("Xg", font).Height;

                    for (int i = 0; i < words.Length; i++)
                    {
                        TextRenderer.DrawText(g, words[i], font, new Point(x, y), Color.Black, flags);
                        Size size = TextRenderer.MeasureText(words[i], font, new Size(int.MaxValue, int.MaxValue), flags);

                        if (words[i] == "INV-2026-0042")
                            linePoint = new Point(x + size.Width / 2, y + height / 2);
                        if (words[i] == "Acme")
                            wordPoint = new Point(x + size.Width / 2, y + height / 2);

                        x += size.Width + 14;
                    }

                    TextRenderer.DrawText(g, "Total: 1,299.00 EUR", font, new Point(20, 110), Color.Black, flags);
                }
            }
            return bitmap;
        }
    }
}
