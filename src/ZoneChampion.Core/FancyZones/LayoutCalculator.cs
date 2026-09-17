using ZoneChampion.Core.Geometry;

namespace ZoneChampion.Core.FancyZones;

/// <summary>
/// Port of FancyZones' zone math (FancyZonesLib/LayoutConfigurator.cpp and Layout.cpp, PowerToys 0.100).
/// Results are relative to the monitor's work-area origin, in physical pixels. Integer math deliberately mirrors
/// the C++ (32-bit longs, truncating division) so zone edges land on exactly the same pixels.
/// </summary>
public static class LayoutCalculator
{
    public const int Multiplier = 10000;
    private const int MaxNegativeSpacing = -20;

    private static readonly FzGridInfo[] PriorityGridLayouts =
    [
        Grid(1, 1, [10000], [10000], [[0]]),
        Grid(1, 2, [10000], [6667, 3333], [[0, 1]]),
        Grid(1, 3, [10000], [2500, 5000, 2500], [[0, 1, 2]]),
        Grid(2, 3, [5000, 5000], [2500, 5000, 2500], [[0, 1, 2], [0, 1, 3]]),
        Grid(2, 3, [5000, 5000], [2500, 5000, 2500], [[0, 1, 2], [3, 1, 4]]),
        Grid(3, 3, [3333, 3334, 3333], [2500, 5000, 2500], [[0, 1, 2], [0, 1, 3], [4, 1, 5]]),
        Grid(3, 3, [3333, 3334, 3333], [2500, 5000, 2500], [[0, 1, 2], [3, 1, 4], [5, 1, 6]]),
        Grid(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 2, 5], [6, 1, 2, 7]]),
        Grid(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 2, 5], [6, 1, 7, 8]]),
        Grid(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 5, 6], [7, 1, 8, 9]]),
        Grid(3, 4, [3333, 3334, 3333], [2500, 2500, 2500, 2500], [[0, 1, 2, 3], [4, 1, 5, 6], [7, 8, 9, 10]]),
    ];

    /// <summary>Computes zone rectangles keyed by FancyZones zone index, sorted by index.</summary>
    /// <param name="custom">The custom layout <paramref name="layout"/> refers to when its type is "custom".</param>
    /// <param name="dpi">Effective DPI of the monitor (only canvas layouts use it).</param>
    public static IReadOnlyList<(int Index, RectI Rect)> Calculate(FzLayout layout, FzCustomLayout? custom, int workAreaWidth, int workAreaHeight, int dpi)
    {
        if (workAreaWidth == 0 || workAreaHeight == 0)
        {
            return [];
        }

        var type = layout.Type.ToLowerInvariant();
        bool isGridType = type is "columns" or "rows" or "grid" or "priority-grid";
        if (layout.ZoneCount < 0 || (layout.ZoneCount == 0 && isGridType))
        {
            return [];
        }

        // v0.100 takes custom-grid spacing from the applied layout; newer builds re-read it from custom-layouts.json,
        // but the editor writes the same values to both.
        int spacing = layout.ShowSpacing ? layout.Spacing : 0;

        return type switch
        {
            "focus" => Focus(workAreaWidth, workAreaHeight, layout.ZoneCount),
            "columns" => Columns(workAreaWidth, workAreaHeight, layout.ZoneCount, spacing),
            "rows" => Rows(workAreaWidth, workAreaHeight, layout.ZoneCount, spacing),
            "grid" => GridTemplate(workAreaWidth, workAreaHeight, layout.ZoneCount, spacing),
            "priority-grid" => PriorityGrid(workAreaWidth, workAreaHeight, layout.ZoneCount, spacing),
            "custom" => Custom(custom, workAreaWidth, workAreaHeight, dpi, spacing),
            _ => [], // "blank" and anything unrecognized
        };
    }

    private static IReadOnlyList<(int, RectI)> Focus(int width, int height, int zoneCount)
    {
        var zones = new ZoneSet();
        int left = 100;
        int top = 100;
        int right = left + (int)(width * 0.4);
        int bottom = top + (int)(height * 0.4);
        int increment = zoneCount <= 1 ? 0 : 50;

        for (int i = 0; i < zoneCount; i++)
        {
            if (!zones.Add(zones.Count, new RectI(left, top, right, bottom)))
            {
                return [];
            }

            left += increment;
            right += increment;
            top += increment;
            bottom += increment;
        }

        return zones.ToList();
    }

    private static IReadOnlyList<(int, RectI)> Columns(int width, int height, int zoneCount, int spacing)
    {
        var zones = new ZoneSet();
        int totalWidth = width - (spacing * (zoneCount + 1));
        int totalHeight = height - (spacing * 2);
        int top = spacing;
        int left = spacing;

        for (int zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
        {
            int right = left + (zoneIndex + 1) * totalWidth / zoneCount - zoneIndex * totalWidth / zoneCount;
            int bottom = totalHeight + spacing;
            if (!zones.Add(zones.Count, new RectI(left, top, right, bottom)))
            {
                return [];
            }

            left = right + spacing;
        }

        return zones.ToList();
    }

    private static IReadOnlyList<(int, RectI)> Rows(int width, int height, int zoneCount, int spacing)
    {
        var zones = new ZoneSet();
        int totalWidth = width - (spacing * 2);
        int totalHeight = height - (spacing * (zoneCount + 1));
        int top = spacing;
        int left = spacing;

        for (int zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
        {
            int right = totalWidth + spacing;
            int bottom = top + (zoneIndex + 1) * totalHeight / zoneCount - zoneIndex * totalHeight / zoneCount;
            if (!zones.Add(zones.Count, new RectI(left, top, right, bottom)))
            {
                return [];
            }

            top = bottom + spacing;
        }

        return zones.ToList();
    }

    private static IReadOnlyList<(int, RectI)> GridTemplate(int width, int height, int zoneCount, int spacing)
    {
        int rows = 1;
        while (zoneCount / rows >= rows)
        {
            rows++;
        }

        rows--;
        int columns = zoneCount / rows;
        if (zoneCount % rows != 0)
        {
            columns++;
        }

        var rowsPercents = new int[rows];
        for (int row = 0; row < rows; row++)
        {
            rowsPercents[row] = Multiplier * (row + 1) / rows - Multiplier * row / rows;
        }

        var columnsPercents = new int[columns];
        for (int col = 0; col < columns; col++)
        {
            columnsPercents[col] = Multiplier * (col + 1) / columns - Multiplier * col / columns;
        }

        var cellChildMap = new int[rows][];
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            cellChildMap[row] = new int[columns];
            for (int col = 0; col < columns; col++)
            {
                cellChildMap[row][col] = index++;
                if (index == zoneCount)
                {
                    index--;
                }
            }
        }

        return GridZones(width, height, Grid(rows, columns, rowsPercents, columnsPercents, cellChildMap), spacing);
    }

    private static IReadOnlyList<(int, RectI)> PriorityGrid(int width, int height, int zoneCount, int spacing)
    {
        if (zoneCount <= 0)
        {
            return [];
        }

        // FancyZones compares with "<", so the 11-zone table entry is never used and 11+ zones fall back to Grid.
        return zoneCount < PriorityGridLayouts.Length
            ? GridZones(width, height, PriorityGridLayouts[zoneCount - 1], spacing)
            : GridTemplate(width, height, zoneCount, spacing);
    }

    private static IReadOnlyList<(int, RectI)> Custom(FzCustomLayout? custom, int width, int height, int dpi, int spacing)
    {
        if (custom?.Canvas is { } canvas)
        {
            return Canvas(canvas, width, height, dpi);
        }

        if (custom?.Grid is { } grid)
        {
            bool shapeIsValid = grid.RowsPercents.Length == grid.Rows
                && grid.ColumnsPercents.Length == grid.Columns
                && grid.CellChildMap.Length == grid.Rows
                && grid.CellChildMap.All(r => r.Length == grid.Columns);
            return shapeIsValid ? GridZones(width, height, grid, spacing) : [];
        }

        return [];
    }

    private static IReadOnlyList<(int, RectI)> Canvas(FzCanvasInfo canvas, int workAreaWidth, int workAreaHeight, int dpi)
    {
        if (canvas.RefWidth == 0 || canvas.RefHeight == 0 || dpi <= 0)
        {
            return [];
        }

        // Canvas zones are stored in 96-DPI units relative to the work area size when the layout was edited.
        // FancyZones does this math in single-precision floats; do the same for identical rounding.
        float width = workAreaWidth;
        float height = workAreaHeight;
        width = width * 96 / dpi;
        height = height * 96 / dpi;

        var zones = new ZoneSet();
        foreach (var zone in canvas.Zones)
        {
            float x = (float)zone.X * width / canvas.RefWidth;
            float y = (float)zone.Y * height / canvas.RefHeight;
            float zoneWidth = (float)zone.Width * width / canvas.RefWidth;
            float zoneHeight = (float)zone.Height * height / canvas.RefHeight;

            x = x * dpi / 96;
            y = y * dpi / 96;
            zoneWidth = zoneWidth * dpi / 96;
            zoneHeight = zoneHeight * dpi / 96;

            if (!zones.Add(zones.Count, new RectI((int)x, (int)y, (int)(x + zoneWidth), (int)(y + zoneHeight))))
            {
                return [];
            }
        }

        return zones.ToList();
    }

    private static IReadOnlyList<(int, RectI)> GridZones(int totalWidth, int totalHeight, FzGridInfo grid, int spacing)
    {
        var rowStart = new int[grid.Rows];
        var rowEnd = new int[grid.Rows];
        int totalPercents = 0;
        for (int row = 0; row < grid.Rows; row++)
        {
            rowStart[row] = totalPercents * totalHeight / Multiplier;
            totalPercents += grid.RowsPercents[row];
            rowEnd[row] = totalPercents * totalHeight / Multiplier;
        }

        var colStart = new int[grid.Columns];
        var colEnd = new int[grid.Columns];
        totalPercents = 0;
        for (int col = 0; col < grid.Columns; col++)
        {
            colStart[col] = totalPercents * totalWidth / Multiplier;
            totalPercents += grid.ColumnsPercents[col];
            colEnd[col] = totalPercents * totalWidth / Multiplier;
        }

        var map = grid.CellChildMap;
        var zones = new ZoneSet();
        for (int row = 0; row < grid.Rows; row++)
        {
            for (int col = 0; col < grid.Columns; col++)
            {
                int i = map[row][col];

                // A zone starts at its top-left cell and extends right and down while the cells share its id.
                if ((row != 0 && map[row - 1][col] == i) || (col != 0 && map[row][col - 1] == i))
                {
                    continue;
                }

                int maxRow = row;
                while (maxRow + 1 < grid.Rows && map[maxRow + 1][col] == i)
                {
                    maxRow++;
                }

                int maxCol = col;
                while (maxCol + 1 < grid.Columns && map[row][maxCol + 1] == i)
                {
                    maxCol++;
                }

                int left = colStart[col] + (col == 0 ? spacing : spacing / 2);
                int top = rowStart[row] + (row == 0 ? spacing : spacing / 2);
                int right = colEnd[maxCol] - (maxCol == grid.Columns - 1 ? spacing : spacing / 2);
                int bottom = rowEnd[maxRow] - (maxRow == grid.Rows - 1 ? spacing : spacing / 2);

                if (!zones.Add(i, new RectI(left, top, right, bottom)))
                {
                    return [];
                }
            }
        }

        return zones.ToList();
    }

    private static FzGridInfo Grid(int rows, int columns, int[] rowsPercents, int[] columnsPercents, int[][] cellChildMap) =>
        new(rows, columns, rowsPercents, columnsPercents, cellChildMap, ShowSpacing: true, Spacing: 0);

    /// <summary>Zones keyed by id. Like FancyZones, one invalid or duplicate zone invalidates the whole layout.</summary>
    private sealed class ZoneSet
    {
        private readonly SortedDictionary<int, RectI> _zones = new();

        public int Count => _zones.Count;

        public bool Add(int id, RectI rect)
        {
            bool valid = rect.Left >= MaxNegativeSpacing && rect.Right >= MaxNegativeSpacing
                && rect.Top >= MaxNegativeSpacing && rect.Bottom >= MaxNegativeSpacing
                && rect.Width >= 0 && rect.Height >= 0;
            return valid && _zones.TryAdd(id, rect);
        }

        public IReadOnlyList<(int, RectI)> ToList() => _zones.Select(z => (z.Key, z.Value)).ToList();
    }
}
