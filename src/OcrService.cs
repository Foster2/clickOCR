using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ClickOCR
{
    /// <summary>Thin wrapper around the OCR engine that ships with Windows.</summary>
    internal sealed class OcrService
    {
        private readonly OcrEngine _engine;
        private readonly object _gate = new object();

        internal string LanguageName
        {
            get { return _engine.RecognizerLanguage.DisplayName; }
        }

        internal OcrService(string languageTag)
        {
            OcrEngine engine = null;

            if (!string.IsNullOrEmpty(languageTag) && languageTag.Trim().Length > 0)
            {
                try
                {
                    engine = OcrEngine.TryCreateFromLanguage(new Language(languageTag.Trim()));
                    if (engine == null)
                        Logger.Write("OCR language '" + languageTag + "' is not installed; falling back.");
                }
                catch (Exception ex)
                {
                    Logger.Write("OCR language '" + languageTag + "' rejected: " + ex.Message);
                }
            }

            if (engine == null)
            {
                try { engine = OcrEngine.TryCreateFromUserProfileLanguages(); }
                catch (Exception ex) { Logger.Write("TryCreateFromUserProfileLanguages: " + ex.Message); }
            }

            if (engine == null)
            {
                try
                {
                    var available = OcrEngine.AvailableRecognizerLanguages;
                    if (available != null && available.Count > 0)
                        engine = OcrEngine.TryCreateFromLanguage(available[0]);
                }
                catch (Exception ex) { Logger.Write("AvailableRecognizerLanguages: " + ex.Message); }
            }

            if (engine == null)
                throw new InvalidOperationException(
                    "No Windows OCR language pack is available. Add a language with OCR support in " +
                    "Settings > Time & Language > Language.");

            _engine = engine;
            Logger.Write("OCR engine: " + LanguageName + " (" + _engine.RecognizerLanguage.LanguageTag + ")");
        }

        internal OcrResult Recognize(Bitmap bitmap)
        {
            if (bitmap == null) throw new ArgumentNullException("bitmap");

            using (SoftwareBitmap software = ToSoftwareBitmap(bitmap))
            {
                lock (_gate)
                {
                    var operation = _engine.RecognizeAsync(software).AsTask();
                    if (!operation.Wait(TimeSpan.FromSeconds(20)))
                        throw new TimeoutException("OCR timed out.");

                    return operation.Result;
                }
            }
        }

        /// <summary>Copies a 32bpp bitmap into the BGRA8 buffer the OCR engine expects.</summary>
        private static SoftwareBitmap ToSoftwareBitmap(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            byte[] pixels = new byte[width * height * 4];

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int rowBytes = width * 4;
                for (int y = 0; y < height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowBytes, rowBytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(),
                BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        }
    }
}
