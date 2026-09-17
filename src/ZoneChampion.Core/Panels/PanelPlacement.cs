using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Settings;

namespace ZoneChampion.Core.Panels;

public static class PanelPlacement
{
    /// <summary>
    /// Resolves <see cref="PanelEdge.Auto"/> to the left or right side of the zone, whichever is nearer the edge of
    /// the monitor. A zone centered on the monitor gets the left side.
    /// </summary>
    public static PanelEdge ResolveEdge(PanelEdge configured, RectI zone, RectI workArea)
    {
        if (configured != PanelEdge.Auto)
        {
            return configured;
        }

        int leftGap = zone.Left - workArea.Left;
        int rightGap = workArea.Right - zone.Right;
        return rightGap < leftGap ? PanelEdge.Right : PanelEdge.Left;
    }

    public static bool IsVertical(PanelEdge edge) => edge is PanelEdge.Left or PanelEdge.Right;

    /// <summary>Computes where the panel window's top-left corner goes, in physical pixels.</summary>
    /// <param name="zone">Zone bounds in physical pixels.</param>
    /// <param name="edge">A resolved edge (not <see cref="PanelEdge.Auto"/>).</param>
    /// <param name="windowSize">Panel window size in physical pixels, including <paramref name="inset"/>.</param>
    /// <param name="scale">Monitor DPI scale (1.0 = 96 DPI).</param>
    /// <param name="edgeOffset">DIPs between the zone edge and the visible panel.</param>
    /// <param name="alignmentOffset">DIPs to shift along the edge (positive = down or right).</param>
    /// <param name="inset">DIPs of transparent margin around the visible panel (room for the shadow).</param>
    public static PointI ComputeOrigin(
        RectI zone,
        PanelEdge edge,
        PanelAlignment alignment,
        SizeI windowSize,
        double scale,
        double edgeOffset,
        double alignmentOffset,
        double inset)
    {
        int insetPx = (int)Math.Round(inset * scale);
        int edgePx = (int)Math.Round(edgeOffset * scale);
        int alongPx = (int)Math.Round(alignmentOffset * scale);

        int along = IsVertical(edge)
            ? Align(zone.Top, zone.Bottom, windowSize.Height, insetPx, alignment) + alongPx
            : Align(zone.Left, zone.Right, windowSize.Width, insetPx, alignment) + alongPx;

        return edge switch
        {
            PanelEdge.Left => new PointI(zone.Left + edgePx - insetPx, along),
            PanelEdge.Right => new PointI(zone.Right - edgePx - windowSize.Width + insetPx, along),
            PanelEdge.Top => new PointI(along, zone.Top + edgePx - insetPx),
            PanelEdge.Bottom => new PointI(along, zone.Bottom - edgePx - windowSize.Height + insetPx),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Resolve the edge before placing the panel."),
        };
    }

    private static int Align(int start, int end, int length, int inset, PanelAlignment alignment) => alignment switch
    {
        PanelAlignment.Start => start - inset,
        PanelAlignment.End => end - length + inset,
        _ => start + (end - start - length) / 2,
    };
}
