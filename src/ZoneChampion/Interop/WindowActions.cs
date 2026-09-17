using System.Windows.Threading;
using ZoneChampion.Core.Geometry;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

/// <summary>Things the panel does to other apps' windows. All calls are asynchronous so a hung app can't freeze us.</summary>
internal static class WindowActions
{
    /// <summary>Taskbar behavior: focus the window, or minimize it if it already has focus.</summary>
    public static void ToggleFocus(nint hwnd)
    {
        if (GetForegroundWindow() == hwnd && !IsIconic(hwnd))
        {
            ShowWindowAsync(hwnd, SW_MINIMIZE);
        }
        else
        {
            Activate(hwnd);
        }
    }

    public static void Activate(nint hwnd)
    {
        if (!IsWindow(hwnd))
        {
            return;
        }

        if (IsIconic(hwnd))
        {
            ShowWindowAsync(hwnd, SW_RESTORE);
        }

        if (SetForegroundWindow(hwnd))
        {
            return;
        }

        // Windows only lets a process move focus if it received the last input. Clicks on our no-activate panel
        // normally count; when they don't, briefly sharing input state with the focused window's thread does.
        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint ownThread = GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ownThread || !AttachThreadInput(ownThread, foregroundThread, true))
        {
            return;
        }

        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            AttachThreadInput(ownThread, foregroundThread, false);
        }
    }

    /// <summary>Asks the window to close, exactly like clicking its X (it may prompt to save).</summary>
    public static void Close(nint hwnd) => PostMessage(hwnd, WM_SYSCOMMAND, SC_CLOSE, 0);

    /// <summary>Moves and resizes a window so its visible frame fills <paramref name="target"/>.</summary>
    public static void MoveTo(nint hwnd, RectI target, Dispatcher dispatcher)
    {
        if (IsIconic(hwnd) || IsZoomed(hwnd))
        {
            ShowWindowAsync(hwnd, SW_RESTORE);
        }

        Apply();

        // Crossing onto a monitor with different scaling makes the app rescale itself; settle it once more after.
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            Apply();
        }, dispatcher);
        timer.Start();

        void Apply()
        {
            if (!IsWindow(hwnd) || !GetWindowRect(hwnd, out var outer))
            {
                return;
            }

            // The window rect includes invisible resize borders; grow the target by the same amount.
            var visible = GetVisibleBounds(hwnd);
            int left = visible.Left - outer.Left;
            int top = visible.Top - outer.Top;
            int right = outer.Right - visible.Right;
            int bottom = outer.Bottom - visible.Bottom;

            SetWindowPos(
                hwnd,
                0,
                target.Left - left,
                target.Top - top,
                target.Width + left + right,
                target.Height + top + bottom,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
        }
    }
}
