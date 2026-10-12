using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Screens;

/// <summary>
/// Geometry for the "swap screens" hotkey: every window on one monitor moves to the same relative spot on the other.
/// Pure logic; moving the windows lives in the app.
/// </summary>
public static class ScreenSwap
{
    /// <summary>Which of the two monitors holds most of <paramref name="rect"/>, or null when it's on neither.</summary>
    public static DisplayMonitor? Owner(RectI rect, DisplayMonitor first, DisplayMonitor second)
    {
        long onFirst = rect.IntersectionArea(first.Bounds);
        long onSecond = rect.IntersectionArea(second.Bounds);
        if (onFirst == 0 && onSecond == 0)
        {
            return null;
        }

        return onFirst >= onSecond ? first : second;
    }

    /// <summary>
    /// Moves <paramref name="rect"/> from one monitor to the other, keeping its position and size proportional to the
    /// work area (where zones and maximized windows live). A window covering a whole monitor (full screen) covers the
    /// whole of the other one. A window that started out mostly off <paramref name="from"/> is pulled fully onto
    /// <paramref name="to"/>, so it can't land between or beyond the monitors.
    /// </summary>
    public static RectI Map(RectI rect, DisplayMonitor from, DisplayMonitor to)
    {
        if (rect == from.Bounds)
        {
            return to.Bounds;
        }

        var source = from.WorkArea;
        var target = to.WorkArea;
        double scaleX = (double)target.Width / source.Width;
        double scaleY = (double)target.Height / source.Height;
        var mapped = new RectI(
            target.Left + (int)Math.Round((rect.Left - source.Left) * scaleX),
            target.Top + (int)Math.Round((rect.Top - source.Top) * scaleY),
            target.Left + (int)Math.Round((rect.Right - source.Left) * scaleX),
            target.Top + (int)Math.Round((rect.Bottom - source.Top) * scaleY));

        return Contains(target, mapped.Center) ? mapped : FitInside(mapped, target);
    }

    private static bool Contains(RectI rect, PointI point) =>
        point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;

    private static RectI FitInside(RectI rect, RectI area)
    {
        int width = Math.Min(rect.Width, area.Width);
        int height = Math.Min(rect.Height, area.Height);
        int left = Math.Clamp(rect.Left, area.Left, area.Right - width);
        int top = Math.Clamp(rect.Top, area.Top, area.Bottom - height);
        return RectI.FromSize(left, top, width, height);
    }
}
