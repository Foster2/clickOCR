using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClickOCR
{
    /// <summary>
    /// Tiny local control channel ("ClickOCR.Control") so the tray app can be
    /// driven by scripts (AutoHotkey, Stream Deck, CI) :
    ///
    ///     ClickOCR.exe --send grab
    ///     ClickOCR.exe --send pick 812 540 word
    ///     ClickOCR.exe --send status
    ///
    /// Commands run on the UI thread.
    /// </summary>
    internal sealed class ControlServer : IDisposable
    {
        internal const string PipeName = "ClickOCR.Control";

        private readonly Control _dispatcher;
        private readonly Func<string, string> _handler;
        private Thread _thread;
        private volatile bool _stop;

        internal ControlServer(Control dispatcher, Func<string, string> handler)
        {
            _dispatcher = dispatcher;
            _handler = handler;
        }

        internal void Start()
        {
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "ClickOCR.ControlPipe";
            _thread.Start();
        }

        private void Loop()
        {
            while (!_stop)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    Task waiting = server.WaitForConnectionAsync();
                    while (!waiting.Wait(200))
                    {
                        if (_stop) return;
                    }

                    string command = null;
                    using (StreamReader reader = new StreamReader(server, Encoding.UTF8, false, 1024, true))
                    {
                        command = reader.ReadLine();
                    }

                    string answer = "ERR empty command";
                    if (!string.IsNullOrEmpty(command))
                    {
                        Logger.Write("Control: " + command);
                        answer = Dispatch(command.Trim());
                    }

                    using (StreamWriter writer = new StreamWriter(server, new UTF8Encoding(false), 1024, true))
                    {
                        writer.WriteLine(answer ?? "OK");
                        writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    if (!_stop) Logger.Write("Control pipe: " + ex.Message);
                }
                finally
                {
                    if (server != null)
                    {
                        try { server.Dispose(); } catch { }
                    }
                }
            }
        }

        private string Dispatch(string command)
        {
            string answer = "ERR not handled";
            try
            {
                _dispatcher.Invoke((MethodInvoker)delegate { answer = _handler(command); });
            }
            catch (Exception ex)
            {
                answer = "ERR " + ex.Message;
            }
            return answer;
        }

        public void Dispose()
        {
            _stop = true;
        }
    }
}
