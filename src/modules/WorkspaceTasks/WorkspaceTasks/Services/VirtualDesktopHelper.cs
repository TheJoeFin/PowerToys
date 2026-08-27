// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.Win32;

namespace WorkspaceTasks.Services
{
    /// <summary>
    /// Creates and switches to a new Windows virtual desktop so a workspace can be opened in a
    /// fresh "working space" without disturbing the current one.
    /// </summary>
    /// <remarks>
    /// Windows exposes no documented API to <em>create</em> a virtual desktop — the documented
    /// <c>IVirtualDesktopManager</c> can only query/move windows, and the internal interface that
    /// can create desktops changes GUIDs with almost every Windows build. PowerToys itself only
    /// uses the documented interface plus the registry (see the WindowWalker
    /// <c>VirtualDesktopHelper</c>). We follow the same principle here: create + switch via the
    /// stable system hotkey (Win+Ctrl+D) using documented <c>user32</c> input, and confirm the
    /// result by reading the same registry keys PowerToys reads.
    /// </remarks>
    internal static partial class VirtualDesktopHelper
    {
        private const byte VkLeftWindows = 0x5B;
        private const byte VkControl = 0x11;
        private const byte VkD = 0x44;
        private const uint KeyEventKeyUp = 0x0002;

        private const string Win11VirtualDesktopsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";
        private const string DesktopIdsValue = "VirtualDesktopIDs";
        private const string CurrentDesktopValue = "CurrentVirtualDesktop";

        /// <summary>
        /// Creates a new virtual desktop, switches to it, and optionally names it, confirming the
        /// change via the registry.
        /// </summary>
        /// <param name="desktopName">
        /// Optional name to give the new desktop (shown in Task View). Ignored if null/empty.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a new desktop was observed (or if desktop state could not be
        /// read, in which case we optimistically assume success after a short settle delay).
        /// </returns>
        public static async Task<bool> CreateAndSwitchToNewDesktopAsync(string desktopName = null)
        {
            var before = GetDesktopCount();

            SendCreateDesktopHotkey();

            // If we cannot read desktop state, give the shell a moment to settle and assume success.
            if (before == 0)
            {
                await Task.Delay(700).ConfigureAwait(false);
                TryNameCurrentDesktop(desktopName);
                return true;
            }

            // Poll until the registry reflects the additional desktop (or we time out).
            for (var elapsed = 0; elapsed < 2500; elapsed += 100)
            {
                await Task.Delay(100).ConfigureAwait(false);
                if (GetDesktopCount() > before)
                {
                    // Give the switch animation a brief moment to complete before windows launch.
                    await Task.Delay(250).ConfigureAwait(false);
                    TryNameCurrentDesktop(desktopName);
                    return true;
                }
            }

            return false;
        }

        private static void SendCreateDesktopHotkey()
        {
            // Win down, Ctrl down, D down, then release in reverse order.
            KeybdEvent(VkLeftWindows, 0, 0, UIntPtr.Zero);
            KeybdEvent(VkControl, 0, 0, UIntPtr.Zero);
            KeybdEvent(VkD, 0, 0, UIntPtr.Zero);
            KeybdEvent(VkD, 0, KeyEventKeyUp, UIntPtr.Zero);
            KeybdEvent(VkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
            KeybdEvent(VkLeftWindows, 0, KeyEventKeyUp, UIntPtr.Zero);
        }

        /// <summary>
        /// Names the desktop that is currently visible (i.e. the one we just created and switched to).
        /// Windows reads desktop names from this registry location, so writing it renames the desktop.
        /// </summary>
        private static void TryNameCurrentDesktop(string desktopName)
        {
            if (string.IsNullOrWhiteSpace(desktopName))
            {
                return;
            }

            var id = GetCurrentDesktopId();
            if (id == Guid.Empty)
            {
                return;
            }

            try
            {
                var path = $@"{Win11VirtualDesktopsKey}\Desktops\{{{id.ToString().ToUpperInvariant()}}}";
                using var key = Registry.CurrentUser.CreateSubKey(path);
                key?.SetValue("Name", desktopName, RegistryValueKind.String);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            {
                Debug.WriteLine($"[WorkspaceTasks] Could not name virtual desktop: {ex.Message}");
            }
        }

        private static Guid GetCurrentDesktopId()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Win11VirtualDesktopsKey, false))
                {
                    if (key?.GetValue(CurrentDesktopValue) is byte[] id && id.Length == 16)
                    {
                        return new Guid(id);
                    }
                }

                // Windows 10 stores the current desktop per session.
                var sessionId = Process.GetCurrentProcess().SessionId;
                var sessionKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{sessionId}\VirtualDesktops";
                using (var sessionKey = Registry.CurrentUser.OpenSubKey(sessionKeyPath, false))
                {
                    if (sessionKey?.GetValue(CurrentDesktopValue) is byte[] sessionId16 && sessionId16.Length == 16)
                    {
                        return new Guid(sessionId16);
                    }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[WorkspaceTasks] Could not read current virtual desktop: {ex.Message}");
            }

            return Guid.Empty;
        }

        private static int GetDesktopCount()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Win11VirtualDesktopsKey, false))
                {
                    if (key?.GetValue(DesktopIdsValue) is byte[] ids && ids.Length > 0)
                    {
                        return ids.Length / 16;
                    }
                }

                // Windows 10 stores the list per session.
                var sessionId = Process.GetCurrentProcess().SessionId;
                var sessionKeyPath = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{sessionId}\VirtualDesktops";
                using (var sessionKey = Registry.CurrentUser.OpenSubKey(sessionKeyPath, false))
                {
                    if (sessionKey?.GetValue(DesktopIdsValue) is byte[] sessionIds && sessionIds.Length > 0)
                    {
                        return sessionIds.Length / 16;
                    }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[WorkspaceTasks] Could not read virtual desktop registry: {ex.Message}");
            }

            return 0;
        }

        [LibraryImport("user32.dll", EntryPoint = "keybd_event")]
        private static partial void KeybdEvent(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
