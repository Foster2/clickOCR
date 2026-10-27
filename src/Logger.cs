using System;
using System.IO;

namespace ClickOCR
{
    internal static class Logger
    {
        private static readonly object Gate = new object();
        private static bool _trimmed;

        internal static readonly string Folder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickOCR");

        internal static readonly string FilePath = Path.Combine(Folder, "log.txt");

        internal static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    if (!_trimmed)
                    {
                        _trimmed = true;
                        FileInfo fi = new FileInfo(FilePath);
                        if (fi.Exists && fi.Length > 512 * 1024)
                        {
                            try { File.Delete(FilePath); } catch { }
                        }
                    }
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never break the app.
            }
        }
    }
}
