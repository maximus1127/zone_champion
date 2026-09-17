using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ZoneChampion.Core.Panels;
using ZoneChampion.Core.Settings;

namespace ZoneChampion.Panels;

/// <summary>A <see cref="PanelStyle"/> converted into ready-to-bind WPF values for one panel edge.</summary>
internal sealed class PanelTheme
{
    private const double IndicatorThickness = 3;
    private const double PopupGap = 8;

    public PanelTheme(PanelStyle style, PanelEdge edge)
    {
        Style = style;
        Edge = edge;
        bool vertical = PanelPlacement.IsVertical(edge);

        Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        PanelBackground = Brush(style.Background, style.BackgroundOpacity);
        PanelBorder = Brush(style.BorderColor, style.BorderOpacity);
        PanelBorderThickness = new Thickness(style.BorderThickness);
        PanelCornerRadius = new CornerRadius(style.CornerRadius);

        // Tiles get half the item spacing on each side along the panel, so trim that from the panel's end padding.
        double half = style.ItemSpacing / 2;
        double alongPadding = Math.Max(0, style.Padding - half);
        PanelPadding = vertical
            ? new Thickness(style.Padding, alongPadding, style.Padding, alongPadding)
            : new Thickness(alongPadding, style.Padding, alongPadding, style.Padding);
        TileMargin = vertical ? new Thickness(0, half, 0, half) : new Thickness(half, 0, half, 0);
        double border = style.BorderThickness;
        ContentMargin = new Thickness(
            PanelPadding.Left + border, PanelPadding.Top + border, PanelPadding.Right + border, PanelPadding.Bottom + border);

        ShadowEnabled = style.Shadow && style.ShadowSize > 0 && style.ShadowOpacity > 0;
        ShadowBlur = style.ShadowSize;
        ShadowDepth = style.ShadowSize / 4;
        ShadowOpacity = style.ShadowOpacity;

        // Transparent room around the panel so the shadow isn't clipped by the window edge.
        Inset = ShadowEnabled ? Math.Ceiling(style.ShadowSize + ShadowDepth) : 1;

        IconSize = style.IconSize;
        TileSize = style.IconSize + 2 * style.ItemPadding;
        TileCornerRadius = new CornerRadius(style.ItemCornerRadius);
        HoverBrush = Brush(style.HoverColor, style.HoverOpacity);
        ActiveBrush = Brush(style.ActiveColor, style.ActiveOpacity);
        IndicatorBrush = Brush(style.ActiveIndicatorColor, 1);
        ShowIndicator = style.ActiveIndicator;
        MinimizedOpacity = style.MinimizedOpacity;

        double indicatorLength = Math.Round(TileSize * 0.42);
        (IndicatorHorizontal, IndicatorVertical, IndicatorWidth, IndicatorHeight) = edge switch
        {
            PanelEdge.Right => (HorizontalAlignment.Right, VerticalAlignment.Center, IndicatorThickness, indicatorLength),
            PanelEdge.Top => (HorizontalAlignment.Center, VerticalAlignment.Top, indicatorLength, IndicatorThickness),
            PanelEdge.Bottom => (HorizontalAlignment.Center, VerticalAlignment.Bottom, indicatorLength, IndicatorThickness),
            _ => (HorizontalAlignment.Left, VerticalAlignment.Center, IndicatorThickness, indicatorLength),
        };

        // The tag dot sits in the corner away from the screen edge so it doesn't collide with the indicator.
        (TagHorizontal, TagVertical) = edge switch
        {
            PanelEdge.Right => (HorizontalAlignment.Left, VerticalAlignment.Top),
            PanelEdge.Bottom => (HorizontalAlignment.Right, VerticalAlignment.Top),
            PanelEdge.Top => (HorizontalAlignment.Right, VerticalAlignment.Bottom),
            _ => (HorizontalAlignment.Right, VerticalAlignment.Top),
        };
        TagSize = style.TagSize;
        TagMargin = new Thickness(Math.Max(2, style.ItemPadding / 2 - style.TagSize / 4));
        TagStyle = style.TagStyle;
        TagTileOpacity = style.TagTileOpacity;
        TagColors = style.TagColors;

        PopupBackground = Brush(style.PopupBackground, style.PopupBackgroundOpacity);
        PopupForeground = Brush(style.PopupForeground, 1);
        PopupSubtle = Brush(style.PopupForeground, 0.55);
        PopupHover = Brush(style.PopupForeground, 0.10);
        PopupBorder = Brush(style.BorderColor, Math.Max(style.BorderOpacity, 0.08));
        FontFamily = new FontFamily(style.FontFamily);
        FontSize = style.FontSize;

        // Popups open on the side away from the screen edge, clear of the panel's padding.
        double offset = style.Padding + style.BorderThickness + PopupGap;
        (PopupPlacement, PopupHorizontalOffset, PopupVerticalOffset) = edge switch
        {
            PanelEdge.Right => (PlacementMode.Left, -offset, 0.0),
            PanelEdge.Top => (PlacementMode.Bottom, 0.0, offset),
            PanelEdge.Bottom => (PlacementMode.Top, 0.0, -offset),
            _ => (PlacementMode.Right, offset, 0.0),
        };
    }

    public PanelStyle Style { get; }
    public PanelEdge Edge { get; }
    public Orientation Orientation { get; }

    public Brush PanelBackground { get; }
    public Brush PanelBorder { get; }
    public Thickness PanelBorderThickness { get; }
    public CornerRadius PanelCornerRadius { get; }
    public Thickness PanelPadding { get; }
    public Thickness ContentMargin { get; }

    public bool ShadowEnabled { get; }
    public double ShadowBlur { get; }
    public double ShadowDepth { get; }
    public double ShadowOpacity { get; }
    public double Inset { get; }
    public Thickness InsetMargin => new(Inset);

    public double IconSize { get; }
    public double TileSize { get; }
    public Thickness TileMargin { get; }
    public CornerRadius TileCornerRadius { get; }
    public Brush HoverBrush { get; }
    public Brush ActiveBrush { get; }
    public Brush IndicatorBrush { get; }
    public bool ShowIndicator { get; }
    public double MinimizedOpacity { get; }
    public HorizontalAlignment IndicatorHorizontal { get; }
    public VerticalAlignment IndicatorVertical { get; }
    public double IndicatorWidth { get; }
    public double IndicatorHeight { get; }
    public CornerRadius IndicatorCornerRadius => new(IndicatorThickness / 2);

    public HorizontalAlignment TagHorizontal { get; }
    public VerticalAlignment TagVertical { get; }
    public double TagSize { get; }
    public Thickness TagMargin { get; }
    public TagStyle TagStyle { get; }
    public double TagTileOpacity { get; }
    public IReadOnlyList<string> TagColors { get; }

    public Brush PopupBackground { get; }
    public Brush PopupForeground { get; }
    public Brush PopupSubtle { get; }
    public Brush PopupHover { get; }
    public Brush PopupBorder { get; }
    public FontFamily FontFamily { get; }
    public double FontSize { get; }
    public PlacementMode PopupPlacement { get; }
    public double PopupHorizontalOffset { get; }
    public double PopupVerticalOffset { get; }

    public static SolidColorBrush Brush(string color, double opacity)
    {
        var value = ColorValue.Parse(color).WithOpacity(opacity);
        var brush = new SolidColorBrush(Color.FromArgb(value.A, value.R, value.G, value.B));
        brush.Freeze();
        return brush;
    }
}
