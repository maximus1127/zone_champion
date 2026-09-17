using System.Globalization;

namespace ZoneChampion.Core.Settings;

/// <summary>An ARGB color parsed from <c>#RGB</c>, <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</summary>
public readonly record struct ColorValue(byte A, byte R, byte G, byte B)
{
    public static bool TryParse(string? text, out ColorValue color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hex = text.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
        {
            return false;
        }

        color = new ColorValue((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return true;
    }

    public static ColorValue Parse(string? text) =>
        TryParse(text, out var color) ? color : throw new FormatException($"'{text}' is not a color. Use #RRGGBB or #AARRGGBB.");

    /// <summary>Multiplies the color's alpha by <paramref name="opacity"/> (0..1).</summary>
    public ColorValue WithOpacity(double opacity) =>
        this with { A = (byte)Math.Round(A * Math.Clamp(opacity, 0, 1)) };
}
