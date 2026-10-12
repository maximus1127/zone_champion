using System.Runtime.InteropServices;
using System.Windows.Threading;
using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Screens;
using ZoneChampion.Core.Zones;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

/// <summary>
/// Swaps every window between two monitors, keeping each one's relative position, size and state (normal, maximized
/// or minimized). Moves app windows plus the dialogs and palettes they own; desktop widgets, overlays and the shell stay
/// put, as do windows on other virtual desktops. Like <see cref="WindowActions"/>, every call is asynchronous so a hung
/// app can't freeze us, and nothing is activated, so focus and stacking order stay as they were.
/// </summary>
internal static class ScreenSwapper
{
    /// <returns>How many windows were moved.</returns>
    /// <param name="isAppWindow">Whether a window gets a taskbar button (see WindowTracker).</param>
    public static int Swap(DisplayMonitor first, DisplayMonitor second, Func<nint, bool> isAppWindow, Dispatcher dispatcher)
    {
        var windows = new List<nint>();
        EnumWindows((hwnd, _) =>
        {
            if (ShouldMove(hwnd, isAppWindow))
            {
                windows.Add(hwnd);
            }

            return true;
        }, 0);

        return windows.Count(hwnd => Move(hwnd, first, second, dispatcher));
    }

    private static bool ShouldMove(nint hwnd, Func<nint, bool> isAppWindow)
    {
        if (isAppWindow(hwnd))
        {
            return true;
        }

        // A visible window owned by an app window: a dialog, find bar or tool palette that should travel with it.
        if (!IsWindowVisible(hwnd) || IsCloaked(hwnd) || (GetExStyle(hwnd) & WS_EX_TRANSPARENT) != 0)
        {
            return false;
        }

        nint owner = GetWindow(hwnd, GW_OWNER);
        while (owner != 0 && GetWindow(owner, GW_OWNER) is var next and not 0)
        {
            owner = next;
        }

        return owner != 0 && isAppWindow(owner);
    }

    private static bool Move(nint hwnd, DisplayMonitor first, DisplayMonitor second, Dispatcher dispatcher)
    {
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref placement))
        {
            return false;
        }

        // Where the window returns to when restored. Placement uses workspace coordinates for most windows.
        bool workspace = (GetExStyle(hwnd) & WS_EX_TOOLWINDOW) == 0;
        var rawNormal = placement.rcNormalPosition.ToRectI();

        if (IsIconic(hwnd))
        {
            // Only where it restores to changes; it stays minimized.
            if (ScreenSwap.Owner(rawNormal, first, second) is not { } from)
            {
                return false;
            }

            var to = from == first ? second : first;
            var normal = ScreenSwap.Map(ToScreen(rawNormal, from, workspace), from, to);
            placement.rcNormalPosition = ToRect(FromScreen(normal, to, workspace));
            placement.showCmd = SW_SHOWMINNOACTIVE;
            placement.flags |= WPF_ASYNCWINDOWPLACEMENT;
            return SetWindowPlacement(hwnd, ref placement);
        }

        var visible = GetVisibleBounds(hwnd);
        if (ScreenSwap.Owner(visible, first, second) is not { } source)
        {
            return false;
        }

        var target = source == first ? second : first;
        if (!IsZoomed(hwnd))
        {
            WindowActions.MoveTo(hwnd, ScreenSwap.Map(visible, source, target), dispatcher);
            return true;
        }

        // Windows ignores a new restore position while a window is maximized, so restore it (without activating) at
        // the new spot, move it there explicitly (some apps, e.g. WinForms, reapply their own remembered restore
        // position), then maximize it again. All three are queued on the app's own thread, so they run in order.
        var restored = ScreenSwap.Map(ToScreen(rawNormal, source, workspace), source, target);
        placement.rcNormalPosition = ToRect(FromScreen(restored, target, workspace));
        placement.showCmd = SW_SHOWNOACTIVATE;
        placement.flags = (placement.flags & ~WPF_RESTORETOMAXIMIZED) | WPF_ASYNCWINDOWPLACEMENT;
        SetWindowPlacement(hwnd, ref placement);
        SetWindowPos(hwnd, 0, restored.Left, restored.Top, restored.Width, restored.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
        ShowWindowAsync(hwnd, SW_MAXIMIZE);
        return true;
    }

    /// <summary>Workspace coordinates are relative to the work area, so they're off by the taskbar on the top or left.</summary>
    private static RectI ToScreen(RectI rect, DisplayMonitor monitor, bool workspace) =>
        workspace ? rect.Offset(monitor.WorkArea.Left - monitor.Bounds.Left, monitor.WorkArea.Top - monitor.Bounds.Top) : rect;

    private static RectI FromScreen(RectI rect, DisplayMonitor monitor, bool workspace) =>
        workspace ? rect.Offset(monitor.Bounds.Left - monitor.WorkArea.Left, monitor.Bounds.Top - monitor.WorkArea.Top) : rect;

    private static RECT ToRect(RectI rect) => new() { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
}
