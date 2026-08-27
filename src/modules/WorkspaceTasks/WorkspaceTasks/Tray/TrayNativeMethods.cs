// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace WorkspaceTasks.Tray
{
    /// <summary>
    /// Documented Win32 entry points used to host a notification-area (system tray) icon and its
    /// context menu. Struct fields are declared in native order (names are PascalCase since
    /// sequential marshalling ignores them) so no analyzer naming suppressions are needed.
    /// </summary>
    internal static class TrayNativeMethods
    {
        public const int WmApp = 0x8000;
        public const int TrayCallbackMessage = WmApp + 1;

        public const int WmNull = 0x0000;
        public const int WmLeftButtonUp = 0x0202;
        public const int WmRightButtonUp = 0x0205;
        public const int WmContextMenu = 0x007B;

        public const int NimAdd = 0x00000000;
        public const int NimModify = 0x00000001;
        public const int NimDelete = 0x00000002;

        public const int NifMessage = 0x00000001;
        public const int NifIcon = 0x00000002;
        public const int NifTip = 0x00000004;

        public const uint ImageIcon = 1;
        public const uint LrLoadFromFile = 0x00000010;
        public const uint LrDefaultSize = 0x00000040;

        public const uint MfString = 0x00000000;
        public const uint MfSeparator = 0x00000800;

        public const uint TpmReturnCmd = 0x0100;
        public const uint TpmRightButton = 0x0002;
        public const uint TpmNoNotify = 0x0080;

        public const int MenuCommandOpen = 1;
        public const int MenuCommandExit = 2;

        public static readonly IntPtr HwndMessage = new(-3);

        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WNDCLASSEXW
        {
            public int CbSize;
            public uint Style;
            public WndProcDelegate LpfnWndProc;
            public int CbClsExtra;
            public int CbWndExtra;
            public IntPtr HInstance;
            public IntPtr HIcon;
            public IntPtr HCursor;
            public IntPtr HbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LpszClassName;
            public IntPtr HIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATAW
        {
            public int CbSize;
            public IntPtr HWnd;
            public int UID;
            public int UFlags;
            public int UCallbackMessage;
            public IntPtr HIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SzTip;
            public int DwState;
            public int DwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SzInfo;
            public int UTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string SzInfoTitle;
            public int DwInfoFlags;
            public Guid GuidItem;
            public IntPtr HBalloonIcon;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadImageW(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Shell_NotifyIconW(int dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);
    }
}
