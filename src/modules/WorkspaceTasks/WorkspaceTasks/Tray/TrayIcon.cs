// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WorkspaceTasks.Tray
{
    /// <summary>
    /// A notification-area (system tray) icon backed by a message-only window. Left-click raises
    /// <see cref="LeftClicked"/>; right-click shows a small context menu that raises
    /// <see cref="OpenRequested"/> / <see cref="ExitRequested"/>.
    /// </summary>
    /// <remarks>
    /// All events are raised on the thread that created the instance (the UI thread), because the
    /// message-only window's messages are pumped by that thread's dispatcher.
    /// </remarks>
    internal sealed class TrayIcon : IDisposable
    {
        private const int IconId = 1;

        // The delegate must be kept alive for the lifetime of the window or the GC will collect it.
        private readonly TrayNativeMethods.WndProcDelegate _wndProc;
        private readonly string _className;
        private readonly IntPtr _hwnd;
        private readonly IntPtr _hIcon;
        private bool _disposed;

        public TrayIcon(string iconPath, string tooltip)
        {
            _wndProc = WndProc;
            _className = "WorkspaceTasksTray_" + Guid.NewGuid().ToString("N");

            var hInstance = TrayNativeMethods.GetModuleHandleW(null);

            var windowClass = new TrayNativeMethods.WNDCLASSEXW
            {
                CbSize = Marshal.SizeOf<TrayNativeMethods.WNDCLASSEXW>(),
                LpfnWndProc = _wndProc,
                HInstance = hInstance,
                LpszClassName = _className,
            };
            TrayNativeMethods.RegisterClassExW(ref windowClass);

            _hwnd = TrayNativeMethods.CreateWindowExW(0, _className, "WorkspaceTasksTray", 0, 0, 0, 0, 0, TrayNativeMethods.HwndMessage, IntPtr.Zero, hInstance, IntPtr.Zero);

            _hIcon = TrayNativeMethods.LoadImageW(IntPtr.Zero, iconPath, TrayNativeMethods.ImageIcon, 0, 0, TrayNativeMethods.LrLoadFromFile | TrayNativeMethods.LrDefaultSize);

            var data = new TrayNativeMethods.NOTIFYICONDATAW
            {
                CbSize = Marshal.SizeOf<TrayNativeMethods.NOTIFYICONDATAW>(),
                HWnd = _hwnd,
                UID = IconId,
                UFlags = TrayNativeMethods.NifMessage | TrayNativeMethods.NifIcon | TrayNativeMethods.NifTip,
                UCallbackMessage = TrayNativeMethods.TrayCallbackMessage,
                HIcon = _hIcon,
                SzTip = tooltip ?? string.Empty,
                SzInfo = string.Empty,
                SzInfoTitle = string.Empty,
            };
            TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NimAdd, ref data);
        }

        public event EventHandler LeftClicked;

        public event EventHandler OpenRequested;

        public event EventHandler ExitRequested;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            var data = new TrayNativeMethods.NOTIFYICONDATAW
            {
                CbSize = Marshal.SizeOf<TrayNativeMethods.NOTIFYICONDATAW>(),
                HWnd = _hwnd,
                UID = IconId,
                SzTip = string.Empty,
                SzInfo = string.Empty,
                SzInfoTitle = string.Empty,
            };
            TrayNativeMethods.Shell_NotifyIconW(TrayNativeMethods.NimDelete, ref data);

            if (_hwnd != IntPtr.Zero)
            {
                TrayNativeMethods.DestroyWindow(_hwnd);
            }

            if (_hIcon != IntPtr.Zero)
            {
                TrayNativeMethods.DestroyIcon(_hIcon);
            }
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == TrayNativeMethods.TrayCallbackMessage)
            {
                var mouseMessage = (int)(lParam.ToInt64() & 0xFFFF);
                switch (mouseMessage)
                {
                    case TrayNativeMethods.WmLeftButtonUp:
                        LeftClicked?.Invoke(this, EventArgs.Empty);
                        break;
                    case TrayNativeMethods.WmRightButtonUp:
                    case TrayNativeMethods.WmContextMenu:
                        ShowContextMenu();
                        break;
                }

                return IntPtr.Zero;
            }

            return TrayNativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            if (!TrayNativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var menu = TrayNativeMethods.CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                TrayNativeMethods.AppendMenuW(menu, TrayNativeMethods.MfString, (UIntPtr)TrayNativeMethods.MenuCommandOpen, "Open Workspace Tasks");
                TrayNativeMethods.AppendMenuW(menu, TrayNativeMethods.MfSeparator, UIntPtr.Zero, null);
                TrayNativeMethods.AppendMenuW(menu, TrayNativeMethods.MfString, (UIntPtr)TrayNativeMethods.MenuCommandExit, "Exit");

                // Required so the menu dismisses correctly when the user clicks elsewhere.
                TrayNativeMethods.SetForegroundWindow(_hwnd);

                var command = TrayNativeMethods.TrackPopupMenuEx(
                    menu,
                    TrayNativeMethods.TpmReturnCmd | TrayNativeMethods.TpmRightButton | TrayNativeMethods.TpmNoNotify,
                    cursor.X,
                    cursor.Y,
                    _hwnd,
                    IntPtr.Zero);

                TrayNativeMethods.PostMessageW(_hwnd, TrayNativeMethods.WmNull, IntPtr.Zero, IntPtr.Zero);

                switch (command)
                {
                    case TrayNativeMethods.MenuCommandOpen:
                        OpenRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case TrayNativeMethods.MenuCommandExit:
                        ExitRequested?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkspaceTasks] Tray context menu failed: {ex.Message}");
            }
            finally
            {
                TrayNativeMethods.DestroyMenu(menu);
            }
        }
    }
}
