using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Mojinloop.Desktop;

namespace Mojinloop.InputActivity
{
    public sealed class WindowsGlobalInputActivitySource : IGlobalInputActivitySource
    {
        public event Action ActivityDetected;
        private static WindowsGlobalInputActivitySource active;
        private static readonly WindowsNativeMethods.HookProc KeyboardProc = KeyboardCallback;
        private static readonly WindowsNativeMethods.HookProc MouseProc = MouseCallback;
        private IntPtr keyboardHook, mouseHook;
        private int pending;

        public void StartListening()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (keyboardHook != IntPtr.Zero) return;
            active = this;
            var module = WindowsHookModule.GetModuleHandle(null);
            keyboardHook = WindowsNativeMethods.SetWindowsHookEx(WindowsNativeMethods.WH_KEYBOARD_LL, KeyboardProc, module, 0);
            mouseHook = WindowsNativeMethods.SetWindowsHookEx(WindowsNativeMethods.WH_MOUSE_LL, MouseProc, module, 0);
            if (keyboardHook == IntPtr.Zero || mouseHook == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                StopListening();
                throw new Win32Exception(error, $"Unable to install global input hooks (Win32 {error}).");
            }
#endif
        }

        public void Poll() { if (Interlocked.Exchange(ref pending, 0) != 0) ActivityDetected?.Invoke(); }

        [AOT.MonoPInvokeCallback(typeof(WindowsNativeMethods.HookProc))]
        private static IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
        {
            var current = active;
            int msg = message.ToInt32();
            bool down = msg == WindowsNativeMethods.WM_KEYDOWN || msg == WindowsNativeMethods.WM_SYSKEYDOWN;
            int flags = code >= 0 ? Marshal.ReadInt32(data, 8) : 0;
            if (current != null && code >= 0 && down && (flags & 0x12) == 0) Interlocked.Exchange(ref current.pending, 1);
            return WindowsNativeMethods.CallNextHookEx(current?.keyboardHook ?? IntPtr.Zero, code, message, data);
        }

        [AOT.MonoPInvokeCallback(typeof(WindowsNativeMethods.HookProc))]
        private static IntPtr MouseCallback(int code, IntPtr message, IntPtr data)
        {
            var current = active;
            int msg = message.ToInt32();
            bool activity = msg == WindowsNativeMethods.WM_LBUTTONDOWN || msg == WindowsNativeMethods.WM_RBUTTONDOWN || msg == WindowsNativeMethods.WM_MBUTTONDOWN || msg == WindowsNativeMethods.WM_MOUSEWHEEL;
            int flags = code >= 0 ? Marshal.ReadInt32(data, 12) : 0;
            if (current != null && code >= 0 && activity && (flags & 0x03) == 0) Interlocked.Exchange(ref current.pending, 1);
            return WindowsNativeMethods.CallNextHookEx(current?.mouseHook ?? IntPtr.Zero, code, message, data);
        }

        public void StopListening()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (keyboardHook != IntPtr.Zero) WindowsNativeMethods.UnhookWindowsHookEx(keyboardHook);
            if (mouseHook != IntPtr.Zero) WindowsNativeMethods.UnhookWindowsHookEx(mouseHook);
#endif
            keyboardHook = mouseHook = IntPtr.Zero;
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
