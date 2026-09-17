using System.Text.Json;

namespace ZoneChampion.Core.FancyZones;

/// <summary>Identifies a monitor the way FancyZones' applied-layouts.json does.</summary>
public sealed record FzDevice(string MonitorId, string MonitorInstance, string SerialNumber, int MonitorNumber, Guid VirtualDesktop);

/// <summary>A layout reference: a built-in template ("priority-grid", "columns", ...) or "custom" plus a uuid.</summary>
public sealed record FzLayout(string Type, Guid Uuid, bool ShowSpacing, int Spacing, int ZoneCount);

public sealed record FzAppliedLayout(FzDevice Device, FzLayout Layout);

public sealed record FzDefaultLayout(string MonitorConfiguration, FzLayout Layout);

public sealed record FzGridInfo(
    int Rows,
    int Columns,
    int[] RowsPercents,
    int[] ColumnsPercents,
    int[][] CellChildMap,
    bool ShowSpacing,
    int Spacing);

public sealed record FzCanvasZone(int X, int Y, int Width, int Height);

public sealed record FzCanvasInfo(int RefWidth, int RefHeight, FzCanvasZone[] Zones);

/// <summary>A layout from custom-layouts.json. Exactly one of <see cref="Grid"/> or <see cref="Canvas"/> is set.</summary>
public sealed record FzCustomLayout(Guid Uuid, string Name, string Type, FzGridInfo? Grid, FzCanvasInfo? Canvas);

/// <summary>The parts of FancyZones' JSON settings that decide where zones are.</summary>
public sealed class FancyZonesData
{
    public const string AppliedLayoutsFile = "applied-layouts.json";
    public const string CustomLayoutsFile = "custom-layouts.json";
    public const string DefaultLayoutsFile = "default-layouts.json";

    public IReadOnlyList<FzAppliedLayout> AppliedLayouts { get; init; } = [];
    public IReadOnlyList<FzCustomLayout> CustomLayouts { get; init; } = [];
    public IReadOnlyList<FzDefaultLayout> DefaultLayouts { get; init; } = [];

    /// <summary>False when the FancyZones data folder doesn't exist (PowerToys not installed or never run).</summary>
    public bool FolderFound { get; init; } = true;

    /// <summary>Problems hit while reading; the data that could be read is still returned.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "FancyZones");

    public static FancyZonesData Load(string folder)
    {
        var warnings = new List<string>();
        return new FancyZonesData
        {
            FolderFound = Directory.Exists(folder),
            AppliedLayouts = ReadFile(folder, AppliedLayoutsFile, "applied-layouts", ParseApplied, warnings),
            CustomLayouts = ReadFile(folder, CustomLayoutsFile, "custom-layouts", ParseCustom, warnings),
            DefaultLayouts = ReadFile(folder, DefaultLayoutsFile, "default-layouts", ParseDefault, warnings),
            Warnings = warnings,
        };
    }

    public static FancyZonesData Parse(string? appliedJson, string? customJson, string? defaultJson)
    {
        var warnings = new List<string>();
        return new FancyZonesData
        {
            AppliedLayouts = ParseText(appliedJson, AppliedLayoutsFile, "applied-layouts", ParseApplied, warnings),
            CustomLayouts = ParseText(customJson, CustomLayoutsFile, "custom-layouts", ParseCustom, warnings),
            DefaultLayouts = ParseText(defaultJson, DefaultLayoutsFile, "default-layouts", ParseDefault, warnings),
            Warnings = warnings,
        };
    }

    public FzCustomLayout? FindCustomLayout(Guid uuid) => CustomLayouts.FirstOrDefault(l => l.Uuid == uuid);

    private static List<T> ReadFile<T>(string folder, string fileName, string arrayName, Func<JsonElement, T> parse, List<string> warnings)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            return [];
        }

        string text;
        try
        {
            // FancyZones may be writing the file; share access so we don't block it.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            warnings.Add($"{fileName}: {ex.Message}");
            return [];
        }

        return ParseText(text, fileName, arrayName, parse, warnings);
    }

    private static List<T> ParseText<T>(string? text, string fileName, string arrayName, Func<JsonElement, T> parse, List<string> warnings)
    {
        var results = new List<T>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return results;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                warnings.Add($"{fileName}: no \"{arrayName}\" list");
                return results;
            }

            int index = 0;
            foreach (var item in array.EnumerateArray())
            {
                try
                {
                    results.Add(parse(item));
                }
                catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or FormatException)
                {
                    warnings.Add($"{fileName}: skipped entry {index}: {ex.Message}");
                }

                index++;
            }
        }
        catch (JsonException ex)
        {
            warnings.Add($"{fileName}: {ex.Message}");
        }

        return results;
    }

    private static FzAppliedLayout ParseApplied(JsonElement item)
    {
        var device = item.GetProperty("device");
        return new FzAppliedLayout(
            new FzDevice(
                MonitorId: GetString(device, "monitor"),
                MonitorInstance: GetString(device, "monitor-instance"),
                SerialNumber: GetString(device, "serial-number"),
                MonitorNumber: GetInt(device, "monitor-number"),
                VirtualDesktop: GetGuid(device, "virtual-desktop")),
            ParseLayout(item.GetProperty("applied-layout")));
    }

    private static FzDefaultLayout ParseDefault(JsonElement item) =>
        new(GetString(item, "monitor-configuration"), ParseLayout(item.GetProperty("layout")));

    private static FzLayout ParseLayout(JsonElement layout) => new(
        Type: GetString(layout, "type"),
        Uuid: GetGuid(layout, "uuid"),
        ShowSpacing: GetBool(layout, "show-spacing"),
        Spacing: GetInt(layout, "spacing"),
        ZoneCount: GetInt(layout, "zone-count"));

    private static FzCustomLayout ParseCustom(JsonElement item)
    {
        var type = GetString(item, "type");
        var info = item.GetProperty("info");
        FzGridInfo? grid = null;
        FzCanvasInfo? canvas = null;

        if (type.Equals("grid", StringComparison.OrdinalIgnoreCase))
        {
            grid = new FzGridInfo(
                Rows: GetInt(info, "rows"),
                Columns: GetInt(info, "columns"),
                RowsPercents: GetIntArray(info, "rows-percentage"),
                ColumnsPercents: GetIntArray(info, "columns-percentage"),
                CellChildMap: info.GetProperty("cell-child-map").EnumerateArray()
                    .Select(row => row.EnumerateArray().Select(c => c.GetInt32()).ToArray())
                    .ToArray(),
                ShowSpacing: GetBool(info, "show-spacing"),
                Spacing: GetInt(info, "spacing"));
        }
        else if (type.Equals("canvas", StringComparison.OrdinalIgnoreCase))
        {
            canvas = new FzCanvasInfo(
                RefWidth: GetInt(info, "ref-width"),
                RefHeight: GetInt(info, "ref-height"),
                Zones: info.GetProperty("zones").EnumerateArray()
                    .Select(z => new FzCanvasZone(GetInt(z, "X"), GetInt(z, "Y"), GetInt(z, "width"), GetInt(z, "height")))
                    .ToArray());
        }

        return new FzCustomLayout(GetGuid(item, "uuid"), GetString(item, "name"), type, grid, canvas);
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static Guid GetGuid(JsonElement element, string name) =>
        Guid.TryParse(GetString(element, name), out var guid) ? guid : Guid.Empty;

    private static int[] GetIntArray(JsonElement element, string name) =>
        element.GetProperty(name).EnumerateArray().Select(v => v.GetInt32()).ToArray();
}
