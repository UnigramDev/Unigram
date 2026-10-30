//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Telegram.Host;

namespace Telegram.Navigation
{
    /// <summary>
    /// Every windowed popup - menus, flyouts, tooltips, dropdowns - leaves the pointer showing a
    /// stale system cursor when it opens, whichever shape Windows last drew itself (a resize edge,
    /// the launch spinner), until the mouse moves. The input layer under WinUI recomputes the
    /// cursor when the popup's window appears, its hit test finds no island, and nothing sets one
    /// on the popup's own input source (microsoft-ui-xaml#8829).
    ///
    /// The popups are top-level windows of one class, created once and shown again for every
    /// opening, so one hook on our UI thread sees all of them: when one is shown, the cursor is
    /// put back to what it should be. That holds until the next move, when the framework takes
    /// over again.
    /// </summary>
    public partial class WindowContext
    {
        private const string PopupWindowClass = "Microsoft.UI.Content.PopupWindowSiteBridge";

        // Never unhooked: an out-of-context hook belongs to the thread that set it, and every
        // window lives on this one until the process ends.
        private static IntPtr _popupHook;

        private static void WatchPopupCursor()
        {
            if (_popupHook != IntPtr.Zero)
            {
                return;
            }

            unsafe
            {
                // Out of context: nothing is injected, and the callback is posted to this thread's
                // queue - it runs between messages, on the UI thread.
                _popupHook = Win32.SetWinEventHook(Win32.EVENT_OBJECT_SHOW, Win32.EVENT_OBJECT_SHOW, IntPtr.Zero,
                    (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, int, int, uint, uint, void>)&OnObjectShown,
                    Win32.GetCurrentProcessId(), Win32.GetCurrentThreadId(), Win32.WINEVENT_OUTOFCONTEXT);
            }

            if (_popupHook == IntPtr.Zero)
            {
                Logger.Warning("SetWinEventHook failed, popups will open with a stale cursor");
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static void OnObjectShown(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            // An exception cannot cross back into user32.
            try
            {
                if (idObject == Win32.OBJID_WINDOW && idChild == Win32.CHILDID_SELF && IsPopupWindow(hwnd))
                {
                    RestorePopupCursor();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private static void RestorePopupCursor()
        {
            if (!Win32.GetCursorPos(out var point))
            {
                return;
            }

            var under = Win32.GetAncestor(Win32.WindowFromPoint(point), Win32.GA_ROOT);
            if (under == IntPtr.Zero)
            {
                return;
            }

            // Over a popup - the one just shown, or the menu a submenu opened beside - it is the
            // arrow: what popup content shows, and what the framework sets on the first move.
            // Over a window the pointer was already on, it is what the framework last asked that
            // window's island for, so a tooltip opening over a link leaves the hand alone.
            var cursor = IsPopupWindow(under)
                ? SystemCursor(Win32.IDC_ARROW)
                : FromHandle(under)?.GetIslandCursor() ?? IntPtr.Zero;

            if (cursor != IntPtr.Zero)
            {
                Win32.SetCursor(cursor);
            }
        }

        private static unsafe bool IsPopupWindow(IntPtr hwnd)
        {
            // Room for one character past the name and the terminator: GetClassName truncates, so
            // without it a longer name that starts the same would come back equal.
            var length = PopupWindowClass.Length + 2;
            var buffer = stackalloc char[length];

            var count = Win32.GetClassNameW(hwnd, (IntPtr)buffer, length);
            return count == PopupWindowClass.Length
                && new ReadOnlySpan<char>(buffer, count).SequenceEqual(PopupWindowClass);
        }

        private static WindowContext FromHandle(IntPtr hwnd)
        {
            lock (_allLock)
            {
                foreach (var window in All)
                {
                    if (window._handle == hwnd)
                    {
                        return window;
                    }
                }
            }

            return null;
        }

        private IntPtr GetIslandCursor()
        {
            var island = _window.Content?.XamlRoot?.ContentIsland;
            if (island == null)
            {
                return IntPtr.Zero;
            }

            // The source the framework itself sets the cursor on: there is one per island.
            return InputPointerSource.GetForIsland(island).Cursor is InputSystemCursor cursor
                ? SystemCursor(cursor.CursorShape switch
                {
                    InputSystemCursorShape.Hand => Win32.IDC_HAND,
                    InputSystemCursorShape.IBeam => Win32.IDC_IBEAM,
                    InputSystemCursorShape.SizeWestEast => Win32.IDC_SIZEWE,
                    InputSystemCursorShape.SizeNorthSouth => Win32.IDC_SIZENS,
                    InputSystemCursorShape.SizeNorthwestSoutheast => Win32.IDC_SIZENWSE,
                    InputSystemCursorShape.SizeNortheastSouthwest => Win32.IDC_SIZENESW,
                    InputSystemCursorShape.SizeAll => Win32.IDC_SIZEALL,
                    InputSystemCursorShape.Cross => Win32.IDC_CROSS,
                    InputSystemCursorShape.Wait => Win32.IDC_WAIT,
                    InputSystemCursorShape.AppStarting => Win32.IDC_APPSTARTING,
                    InputSystemCursorShape.UniversalNo => Win32.IDC_NO,
                    InputSystemCursorShape.Help => Win32.IDC_HELP,
                    _ => Win32.IDC_ARROW
                })
                : SystemCursor(Win32.IDC_ARROW);
        }

        // System cursors are shared: LoadCursor hands back the same handle every time, and it is
        // never destroyed.
        private static IntPtr SystemCursor(int id)
        {
            return Win32.LoadCursorW(IntPtr.Zero, id);
        }
    }
}
