namespace ZoneChampion.Core.Geometry;

public readonly record struct PointI(int X, int Y);

public readonly record struct SizeI(int Width, int Height);

/// <summary>Integer rectangle in physical screen pixels. Right and Bottom are exclusive, like a Win32 RECT.</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public long Area => IsEmpty ? 0 : (long)Width * Height;
    public PointI Center => new(Left + Width / 2, Top + Height / 2);

    public static RectI FromSize(int left, int top, int width, int height) => new(left, top, left + width, top + height);

    public RectI Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);

    public long IntersectionArea(RectI other)
    {
        long width = Math.Min(Right, other.Right) - Math.Max(Left, other.Left);
        long height = Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top);
        return width > 0 && height > 0 ? width * height : 0;
    }

    public bool Contains(RectI other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    public override string ToString() => $"({Left},{Top})-({Right},{Bottom}) {Width}x{Height}";
}
