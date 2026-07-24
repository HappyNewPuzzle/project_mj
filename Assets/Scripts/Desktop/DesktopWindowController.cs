using System;
using System.Collections;
using UnityEngine;

namespace Mojinloop.Desktop
{
    [DefaultExecutionOrder(-1000)]
    public sealed class DesktopWindowController : MonoBehaviour
    {
        // A deliberately uncommon RGB value used as the transparent Windows color key.
        static readonly Color TransparentKey = new(1f, 0f, 1f, 1f);
        const uint TransparentKeyColorRef = 0x00FF00FF;

        [SerializeField] DesktopWindowSettings settings;
        IntPtr window;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static readonly WindowsNativeMethods.EnumWindowsProc WindowSearchCallback = FindProcessWindow;
        static uint processId;
        static IntPtr processWindow;
        enum DragMode { None, Move, Resize }
        DragMode dragMode;
        WindowsNativeMethods.Point dragStartCursor;
        WindowsNativeMethods.Rect dragStartWindow;
        bool clickThroughApplied;
#endif

        void Awake()
        {
            Application.runInBackground = true;
            ConfigureCamera();
        }

        IEnumerator Start()
        {
            Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            processId = WindowsNativeMethods.GetCurrentProcessId();
            for (var frame = 0; frame < 60; frame++)
            {
                yield return null;
                window = FindOwnWindow();
                if (window != IntPtr.Zero)
                {
                    ApplyWindow();
                    if (frame >= 12)
                        break;
                }
            }

            if (window == IntPtr.Zero)
                Debug.LogError("Unity window handle was not found.");
#endif
            yield break;
        }

        static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = TransparentKey;
        }

        int WindowWidth => settings != null ? settings.width : 640;
        int WindowHeight => settings != null ? settings.height : 240;
        int RightMargin => settings != null ? settings.rightMargin : 16;
        int BottomMargin => settings != null ? settings.bottomMargin : 8;
        bool IsTopmost => settings == null || settings.topmost;
        bool IsClickThrough => settings == null || settings.clickThrough;
        bool ShowWindowControls => settings == null || settings.showWindowControls;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static IntPtr FindOwnWindow()
        {
            var titledWindow = WindowsNativeMethods.FindWindow(null, Application.productName);
            if (titledWindow != IntPtr.Zero)
            {
                WindowsNativeMethods.GetWindowThreadProcessId(titledWindow, out uint owner);
                if (owner == processId)
                    return titledWindow;
            }

            processWindow = IntPtr.Zero;
            WindowsNativeMethods.EnumWindows(WindowSearchCallback, IntPtr.Zero);
            return processWindow;
        }

        [AOT.MonoPInvokeCallback(typeof(WindowsNativeMethods.EnumWindowsProc))]
        static bool FindProcessWindow(IntPtr candidate, IntPtr parameter)
        {
            WindowsNativeMethods.GetWindowThreadProcessId(candidate, out uint owner);
            if (owner != processId || !WindowsNativeMethods.IsWindowVisible(candidate))
                return true;

            processWindow = candidate;
            return false;
        }

        void ApplyWindow()
        {
            long style = WindowsNativeMethods.GetWindowLongPtr(window, WindowsNativeMethods.GWL_STYLE).ToInt64();
            style = (style & ~0x00CF0000L) | WindowsNativeMethods.WS_POPUP | WindowsNativeMethods.WS_VISIBLE;
            WindowsNativeMethods.SetWindowLongPtr(window, WindowsNativeMethods.GWL_STYLE, new IntPtr(style));

            long ex = WindowsNativeMethods.GetWindowLongPtr(window, WindowsNativeMethods.GWL_EXSTYLE).ToInt64();
            ex |= WindowsNativeMethods.WS_EX_LAYERED | WindowsNativeMethods.WS_EX_TOOLWINDOW;
            if (IsClickThrough)
                ex |= WindowsNativeMethods.WS_EX_TRANSPARENT;
            WindowsNativeMethods.SetWindowLongPtr(window, WindowsNativeMethods.GWL_EXSTYLE, new IntPtr(ex));
            clickThroughApplied = IsClickThrough;

            if (!WindowsNativeMethods.SetLayeredWindowAttributes(
                    window, TransparentKeyColorRef, 255, WindowsNativeMethods.LWA_COLORKEY))
                Debug.LogError("Failed to apply desktop window transparency.");

            var margins = new WindowsNativeMethods.Margins { Left = -1 };
            WindowsNativeMethods.DwmExtendFrameIntoClientArea(window, ref margins);

            if (!WindowsNativeMethods.SystemParametersInfo(
                    WindowsNativeMethods.SPI_GETWORKAREA, 0, out var area, 0))
                area = new WindowsNativeMethods.Rect
                {
                    Right = Display.main.systemWidth,
                    Bottom = Display.main.systemHeight
                };

            bool ok = WindowsNativeMethods.SetWindowPos(
                window,
                IsTopmost ? WindowsNativeMethods.HWND_TOPMOST : IntPtr.Zero,
                area.Right - WindowWidth - RightMargin,
                area.Bottom - WindowHeight - BottomMargin,
                WindowWidth,
                WindowHeight,
                WindowsNativeMethods.SWP_FRAMECHANGED |
                WindowsNativeMethods.SWP_SHOWWINDOW |
                WindowsNativeMethods.SWP_NOACTIVATE);

            if (!ok)
                Debug.LogError("Failed to position desktop window.");
        }

        void Update()
        {
            if (window == IntPtr.Zero || !ShowWindowControls)
                return;

            if (!WindowsNativeMethods.GetCursorPos(out var cursor) ||
                !WindowsNativeMethods.GetWindowRect(window, out var rect))
                return;

            if (dragMode != DragMode.None)
            {
                if ((WindowsNativeMethods.GetAsyncKeyState(WindowsNativeMethods.VK_LBUTTON) & 0x8000) == 0)
                {
                    dragMode = DragMode.None;
                }
                else
                {
                    int dx = cursor.X - dragStartCursor.X;
                    int dy = cursor.Y - dragStartCursor.Y;
                    int width = dragStartWindow.Right - dragStartWindow.Left;
                    int height = dragStartWindow.Bottom - dragStartWindow.Top;
                    int x = dragStartWindow.Left;
                    int y = dragStartWindow.Top;
                    if (dragMode == DragMode.Move)
                    {
                        x += dx;
                        y += dy;
                    }
                    else
                    {
                        width = Mathf.Max(320, width + dx);
                        height = Mathf.Max(120, height + dy);
                    }
                    WindowsNativeMethods.SetWindowPos(window, IntPtr.Zero, x, y, width, height,
                        WindowsNativeMethods.SWP_NOZORDER | WindowsNativeMethods.SWP_NOACTIVATE);
                }
            }

            bool overControls = cursor.X >= rect.Right - 150 && cursor.X < rect.Right &&
                                cursor.Y >= rect.Top && cursor.Y < rect.Top + 30;
            SetRuntimeClickThrough(IsClickThrough && !overControls && dragMode == DragMode.None);
        }

        void SetRuntimeClickThrough(bool enabled)
        {
            if (clickThroughApplied == enabled)
                return;
            long ex = WindowsNativeMethods.GetWindowLongPtr(window, WindowsNativeMethods.GWL_EXSTYLE).ToInt64();
            if (enabled) ex |= WindowsNativeMethods.WS_EX_TRANSPARENT;
            else ex &= ~WindowsNativeMethods.WS_EX_TRANSPARENT;
            WindowsNativeMethods.SetWindowLongPtr(window, WindowsNativeMethods.GWL_EXSTYLE, new IntPtr(ex));
            clickThroughApplied = enabled;
        }

        void OnGUI()
        {
            if (window == IntPtr.Zero || !ShowWindowControls)
                return;

            const float width = 70f;
            var moveRect = new UnityEngine.Rect(Screen.width - width * 2, 0, width, 28);
            var sizeRect = new UnityEngine.Rect(Screen.width - width, 0, width, 28);
            if (GUI.RepeatButton(moveRect, "MOVE") && Event.current.type == EventType.MouseDown)
                BeginDrag(DragMode.Move);
            if (GUI.RepeatButton(sizeRect, "SIZE") && Event.current.type == EventType.MouseDown)
                BeginDrag(DragMode.Resize);
        }

        void BeginDrag(DragMode mode)
        {
            if (!WindowsNativeMethods.GetCursorPos(out dragStartCursor) ||
                !WindowsNativeMethods.GetWindowRect(window, out dragStartWindow))
                return;
            dragMode = mode;
            SetRuntimeClickThrough(false);
        }
#endif
    }
}
