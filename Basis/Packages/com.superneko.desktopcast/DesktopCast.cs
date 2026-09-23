using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace com.superneko.basis.desktopcast
{
    interface Window
    {

    }

    public static class Platform
    {
        public struct WindowRef : Window
        {
            public string Title;
            public IntPtr Handle;
            
            public uint dwStyle;
            public uint dwExStyle;

            public override readonly string ToString()
            {
                var title = string.IsNullOrEmpty(Title) ? "<Unnamed or error>" : Title;

                return $"MSWindows window hwnd: {Handle}, title: {title} dwStyle: {dwStyle:X} dwExStyle: {dwExStyle:X}";
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public override string ToString()
                => $"({Left}, {Top}) - ({Right}, {Bottom})";
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWINFO
        {
            public uint cbSize;
            public RECT rcWindow;
            public RECT rcClient;
            public uint dwStyle;
            public uint dwExStyle;
            public uint dwWindowStatus;
            public uint cxWindowBorders;
            public uint cyWindowBorders;
            public ushort atomWindowType;
            public ushort wCreatorVersion;
        }

        [DllImport("user32.dll")]
        static extern int EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowInfo(IntPtr hwnd, ref WINDOWINFO pwi);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        static extern int GetWindowTextW(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpString, int nMaxCount);

        [return: MarshalAs(UnmanagedType.Bool)]
        delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

        [RuntimeInitializeOnLoadMethod]
        public static void TestEntrypoint()
        {
            Task.Run(async () =>
            {
                var result = await EnumerateWindowToCapture();

                foreach (var window in result)
                {
                    Debug.Log($"Window found: {window}");
                }
            });
        }

        public static async Task<WindowRef[]> EnumerateWindowToCapture()
        {
            List<WindowRef> windowRefs = new();

            EnumWindowsDelegate callback = (hWnd, lParam) =>
            {
                var info = new WINDOWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<WINDOWINFO>()
                };

                if (!GetWindowInfo(hWnd, ref info)) return true;

                const uint WS_VISIBLE = 0x10000000;
                const uint WS_CHILD = 0x40000000;
                const uint WS_POPUP = 0x80000000;
                const uint WS_SYSMENU = 0x00080000;
                const uint WS_EX_TOOLWINDOW = 0x00000080;
                const uint WS_EX_APPWINDOW = 0x00040000;

                // if ((((info.dwStyle & (WS_CHILD | WS_POPUP | WS_SYSMENU)) > 0) || ((info.dwExStyle & WS_EX_TOOLWINDOW) > 0)) && ()) return true;

                var isVisible = IsWindowVisible(hWnd);
                var isChildWindowLike = (info.dwStyle & (WS_CHILD | WS_POPUP)) > 0 || ((info.dwExStyle & WS_EX_TOOLWINDOW) > 0);
                // var isChildWindowLike = false;
                var isForcedInTaskBar = (info.dwExStyle & WS_EX_APPWINDOW) != 0;

                if (isVisible && (!isChildWindowLike || isForcedInTaskBar))
                {
                    windowRefs.Add(new WindowRef { Handle = hWnd, Title = GetWindowTitle(hWnd), dwStyle = info.dwStyle, dwExStyle = info.dwExStyle });
                }

                return true;
            };

            var result = EnumWindows(callback, IntPtr.Zero);

            if (result == 0)
            {
                throw new Exception("Failed to enumerate windows");
            }

            return windowRefs.ToArray();
        }

        static string GetWindowTitle(IntPtr hWnd)
        {
            var titleBulider = new StringBuilder(1024);
            var titleResult = GetWindowTextW(hWnd, titleBulider, titleBulider.Capacity);
            var title = titleResult == 0 ? "" : titleBulider.ToString();

            return title;
        }
    }
}
