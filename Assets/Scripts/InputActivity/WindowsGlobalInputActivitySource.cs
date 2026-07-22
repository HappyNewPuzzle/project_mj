using System;using System.Threading;using Mojinloop.Desktop;namespace Mojinloop.InputActivity{public sealed class WindowsGlobalInputActivitySource:IGlobalInputActivitySource{public event Action ActivityDetected;IntPtr keyboardHook,mouseHook;WindowsNativeMethods.HookProc keyboardProc,mouseProc;int pending;public void StartListening(){
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
if(keyboardHook!=IntPtr.Zero)return;keyboardProc=Keyboard;mouseProc=Mouse;keyboardHook=WindowsNativeMethods.SetWindowsHookEx(WindowsNativeMethods.WH_KEYBOARD_LL,keyboardProc,IntPtr.Zero,0);mouseHook=WindowsNativeMethods.SetWindowsHookEx(WindowsNativeMethods.WH_MOUSE_LL,mouseProc,IntPtr.Zero,0);if(keyboardHook==IntPtr.Zero||mouseHook==IntPtr.Zero){StopListening();throw new InvalidOperationException("Unable to install global input hooks.");}
#endif
}public void Poll(){if(Interlocked.Exchange(ref pending,0)!=0)ActivityDetected?.Invoke();}IntPtr Keyboard(int c,IntPtr w,IntPtr l){int m=w.ToInt32();if(c>=0&&(m==WindowsNativeMethods.WM_KEYDOWN||m==WindowsNativeMethods.WM_SYSKEYDOWN))Interlocked.Exchange(ref pending,1);return WindowsNativeMethods.CallNextHookEx(keyboardHook,c,w,l);}IntPtr Mouse(int c,IntPtr w,IntPtr l){int m=w.ToInt32();if(c>=0&&(m==WindowsNativeMethods.WM_LBUTTONDOWN||m==WindowsNativeMethods.WM_RBUTTONDOWN||m==WindowsNativeMethods.WM_MBUTTONDOWN||m==WindowsNativeMethods.WM_MOUSEWHEEL))Interlocked.Exchange(ref pending,1);return WindowsNativeMethods.CallNextHookEx(mouseHook,c,w,l);}public void StopListening(){
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
if(keyboardHook!=IntPtr.Zero)WindowsNativeMethods.UnhookWindowsHookEx(keyboardHook);if(mouseHook!=IntPtr.Zero)WindowsNativeMethods.UnhookWindowsHookEx(mouseHook);
#endif
keyboardHook=mouseHook=IntPtr.Zero;keyboardProc=mouseProc=null;}}}
