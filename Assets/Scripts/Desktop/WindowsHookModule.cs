using System;
using System.Runtime.InteropServices;

namespace Mojinloop.Desktop
{
    internal static class WindowsHookModule
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string moduleName);
    }
}
