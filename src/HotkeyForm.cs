using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClickOCR
{
    /// <summary>
    /// Invisible window that owns the global hotkey registration and raises
    /// <see cref="HotkeyPressed"/> on the UI thread.
    /// </summary>
    internal sealed class HotkeyForm : Form
    {
        private const int HotkeyId = 0x4C43; // 'LC'

        private string _registeredSpec;

        internal event Action HotkeyPressed;

        internal HotkeyForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);
            AutoScaleMode = AutoScaleMode.None;

            IntPtr forceHandleCreation = Handle;
            GC.KeepAlive(forceHandleCreation);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Never shown: this window exists only to receive WM_HOTKEY.
            base.SetVisibleCore(false);
        }

        internal bool Register(string spec)
        {
            Unregister();

            uint modifiers;
            Keys key;
            if (!NativeMethods.TryParseHotkey(spec, out modifiers, out key))
            {
                Logger.Write("Invalid hotkey: '" + spec + "'");
                return false;
            }

            if (!NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, (uint)key))
            {
                Logger.Write("RegisterHotKey failed for '" + spec + "' (error " + Marshal.GetLastWin32Error() + ")");
                return false;
            }

            _registeredSpec = spec;
            Logger.Write("Hotkey registered: " + spec);
            return true;
        }

        internal void Unregister()
        {
            if (_registeredSpec == null) return;
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registeredSpec = null;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                Action handler = HotkeyPressed;
                if (handler != null) handler();
                return;
            }
            base.WndProc(ref m);
        }
    }
}
