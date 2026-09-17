namespace ZoneChampion.Core.Settings;

public enum PanelEdge
{
    Auto,
    Left,
    Right,
    Top,
    Bottom,
}

public enum PanelAlignment
{
    Start,
    Center,
    End,
}

public enum TagStyle
{
    /// <summary>A small colored dot in the corner of the icon.</summary>
    Dot,

    /// <summary>The icon's tile is tinted with the tag color.</summary>
    Tile,
}

/// <summary>
/// Everything that controls how a panel looks and where it sits. Defaults here must match
/// <see cref="DefaultSettings.Text"/>. Sizes are device-independent pixels (96 = 1 inch at 100% scaling).
/// </summary>
public sealed class PanelStyle
{
    // Placement
    public PanelEdge Edge { get; set; } = PanelEdge.Auto;
    public PanelAlignment Alignment { get; set; } = PanelAlignment.Center;
    public double EdgeOffset { get; set; } = 0;
    public double AlignmentOffset { get; set; } = 0;
    public bool HideWhenEmpty { get; set; } = true;
    public bool HideOnFullscreen { get; set; } = true;
    public bool HideWhenMaximized { get; set; } = true;

    // Panel body
    public string Background { get; set; } = "#1E1E2E";
    public double BackgroundOpacity { get; set; } = 0.85;
    public string BorderColor { get; set; } = "#FFFFFF";
    public double BorderOpacity { get; set; } = 0.10;
    public double BorderThickness { get; set; } = 1;
    public double CornerRadius { get; set; } = 14;
    public double Padding { get; set; } = 5;
    public bool Shadow { get; set; } = true;
    public double ShadowOpacity { get; set; } = 0.35;
    public double ShadowSize { get; set; } = 12;

    // Icons
    public double IconSize { get; set; } = 32;
    public double ItemPadding { get; set; } = 7;
    public double ItemSpacing { get; set; } = 2;
    public double ItemCornerRadius { get; set; } = 10;
    public string HoverColor { get; set; } = "#FFFFFF";
    public double HoverOpacity { get; set; } = 0.10;
    public string ActiveColor { get; set; } = "#89B4FA";
    public double ActiveOpacity { get; set; } = 0.20;
    public bool ActiveIndicator { get; set; } = true;
    public string ActiveIndicatorColor { get; set; } = "#89B4FA";
    public double MinimizedOpacity { get; set; } = 0.45;

    // Hover title and right-click menu
    public bool ShowTitleOnHover { get; set; } = true;
    public int TitleDelayMs { get; set; } = 250;
    public string PopupBackground { get; set; } = "#1E1E2E";
    public double PopupBackgroundOpacity { get; set; } = 0.97;
    public string PopupForeground { get; set; } = "#CDD6F4";
    public string FontFamily { get; set; } = "Segoe UI Variable Text, Segoe UI";
    public double FontSize { get; set; } = 13;

    // Color tags
    public TagStyle TagStyle { get; set; } = TagStyle.Dot;
    public double TagSize { get; set; } = 9;
    public double TagTileOpacity { get; set; } = 0.35;
    public List<string> TagColors { get; set; } =
        ["#F38BA8", "#FAB387", "#F9E2AF", "#A6E3A1", "#94E2D5", "#89B4FA", "#CBA6F7"];

    /// <summary>Returns a problem description for each invalid value (currently: unparseable colors).</summary>
    public IEnumerable<string> Validate()
    {
        (string Name, string Value)[] colors =
        [
            (nameof(Background), Background),
            (nameof(BorderColor), BorderColor),
            (nameof(HoverColor), HoverColor),
            (nameof(ActiveColor), ActiveColor),
            (nameof(ActiveIndicatorColor), ActiveIndicatorColor),
            (nameof(PopupBackground), PopupBackground),
            (nameof(PopupForeground), PopupForeground),
            .. TagColors.Select((c, i) => ($"{nameof(TagColors)}[{i}]", c)),
        ];

        foreach (var (name, value) in colors)
        {
            if (!ColorValue.TryParse(value, out _))
            {
                yield return $"{char.ToLowerInvariant(name[0])}{name[1..]}: '{value}' is not a color (use #RRGGBB or #AARRGGBB)";
            }
        }
    }

    /// <summary>Clamps numbers into ranges that can't break layout.</summary>
    public void Normalize()
    {
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0, 1);
        BorderOpacity = Math.Clamp(BorderOpacity, 0, 1);
        BorderThickness = Math.Clamp(BorderThickness, 0, 20);
        CornerRadius = Math.Clamp(CornerRadius, 0, 200);
        Padding = Math.Clamp(Padding, 0, 100);
        ShadowOpacity = Math.Clamp(ShadowOpacity, 0, 1);
        ShadowSize = Math.Clamp(ShadowSize, 0, 60);
        IconSize = Math.Clamp(IconSize, 12, 256);
        ItemPadding = Math.Clamp(ItemPadding, 0, 60);
        ItemSpacing = Math.Clamp(ItemSpacing, 0, 60);
        ItemCornerRadius = Math.Clamp(ItemCornerRadius, 0, 200);
        HoverOpacity = Math.Clamp(HoverOpacity, 0, 1);
        ActiveOpacity = Math.Clamp(ActiveOpacity, 0, 1);
        MinimizedOpacity = Math.Clamp(MinimizedOpacity, 0, 1);
        TitleDelayMs = Math.Clamp(TitleDelayMs, 0, 5000);
        PopupBackgroundOpacity = Math.Clamp(PopupBackgroundOpacity, 0, 1);
        FontSize = Math.Clamp(FontSize, 6, 48);
        TagSize = Math.Clamp(TagSize, 2, 64);
        TagTileOpacity = Math.Clamp(TagTileOpacity, 0, 1);
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            FontFamily = "Segoe UI";
        }
    }
}
