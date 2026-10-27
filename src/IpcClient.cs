using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace ClickOCR
{
    /// <summary>Command-line client for the control pipe.</summary>
    internal static class IpcClient
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        internal static int Send(string command)
        {
            string response;
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", ControlServer.PipeName, PipeDirection.InOut))
                {
                    client.Connect(3000);

                    using (StreamWriter writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true))
                    {
                        writer.WriteLine(command);
                        writer.Flush();
                    }

                    using (StreamReader reader = new StreamReader(client, Encoding.UTF8, false, 1024, true))
                    {
                        response = reader.ReadLine();
                    }
                }
            }
            catch (Exception ex)
            {
                response = "ERR " + ex.Message;
            }

            Write(response);
            return response != null && response.StartsWith("OK", StringComparison.Ordinal) ? 0 : 1;
        }

        private static void Write(string response)
        {
            try
            {
                Directory.CreateDirectory(Logger.Folder);
                File.WriteAllText(Path.Combine(Logger.Folder, "control-last.txt"), response + Environment.NewLine);
            }
            catch (Exception) { }

            try
            {
                // Works when launched from a console even though this is a GUI exe.
                if (AttachConsole(-1))
                {
                    StreamWriter stdout = new StreamWriter(Console.OpenStandardOutput());
                    stdout.AutoFlush = true;
                    stdout.WriteLine(response);
                    Console.SetOut(stdout);
                }
            }
            catch (Exception) { }
        }
    }
}
