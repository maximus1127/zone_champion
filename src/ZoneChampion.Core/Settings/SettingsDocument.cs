using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ZoneChampion.Core.Settings;

public sealed class SettingsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>A zone-specific override from the "zones" list.</summary>
/// <param name="Monitor">FancyZones monitor id or instance id; null matches every monitor.</param>
/// <param name="Zone">1-based zone number; null matches every zone.</param>
/// <param name="Style">Panel settings to layer over the base "panel" settings.</param>
public sealed record ZoneOverride(string? Monitor, int? Zone, bool? Enabled, JsonObject Style)
{
    public bool Matches(string monitorId, string monitorInstanceId, int zoneNumber) =>
        (Monitor is null
            || string.Equals(Monitor, monitorId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Monitor, monitorInstanceId, StringComparison.OrdinalIgnoreCase))
        && (Zone is null || Zone == zoneNumber);
}

public sealed record ResolvedPanelSettings(bool Enabled, PanelStyle Style);

/// <summary>A parsed and validated settings file.</summary>
public sealed class SettingsDocument
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly JsonObject _panel;
    private readonly Dictionary<(string, string, int), ResolvedPanelSettings> _cache = new();

    private SettingsDocument(bool startWithWindows, string? fancyZonesDataFolder, JsonObject panel, IReadOnlyList<ZoneOverride> overrides)
    {
        StartWithWindows = startWithWindows;
        FancyZonesDataFolder = fancyZonesDataFolder;
        _panel = panel;
        Overrides = overrides;
    }

    public bool StartWithWindows { get; }
    public string? FancyZonesDataFolder { get; }
    public IReadOnlyList<ZoneOverride> Overrides { get; }

    public static SettingsDocument Default { get; } = Parse(DefaultSettings.Text);

    /// <exception cref="SettingsException">The text isn't valid settings; the message says what's wrong.</exception>
    public static SettingsDocument Parse(string json)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: DocumentOptions) as JsonObject
                ?? throw new SettingsException("The settings file must contain a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new SettingsException($"The settings file isn't valid JSON: {ex.Message}", ex);
        }

        bool startWithWindows = true;
        string? fancyZonesDataFolder = null;
        var panel = new JsonObject();
        var overrides = new List<ZoneOverride>();

        foreach (var (name, value) in root)
        {
            switch (name.ToLowerInvariant())
            {
                case "startwithwindows":
                    startWithWindows = Read<bool>(value, name);
                    break;
                case "fancyzonesdatafolder":
                    fancyZonesDataFolder = Read<string?>(value, name);
                    break;
                case "panel":
                    panel = value as JsonObject ?? throw new SettingsException("\"panel\" must be an object.");
                    break;
                case "zones":
                    var zones = value as JsonArray ?? throw new SettingsException("\"zones\" must be a list.");
                    for (int i = 0; i < zones.Count; i++)
                    {
                        overrides.Add(ParseOverride(zones[i], i));
                    }

                    break;
                default:
                    throw new SettingsException($"Unknown setting \"{name}\".");
            }
        }

        var document = new SettingsDocument(startWithWindows, fancyZonesDataFolder, (JsonObject)panel.DeepClone(), overrides);

        // Surface mistakes now rather than when a matching zone shows up.
        BuildStyle(document._panel, "panel");
        for (int i = 0; i < overrides.Count; i++)
        {
            var merged = (JsonObject)document._panel.DeepClone();
            Overlay(merged, overrides[i].Style);
            BuildStyle(merged, $"zones[{i}]");
        }

        return document;
    }

    /// <summary>Merges the base panel style with every matching zone override.</summary>
    public ResolvedPanelSettings Resolve(string monitorId, string monitorInstanceId, int zoneNumber)
    {
        var key = (monitorId, monitorInstanceId, zoneNumber);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        bool enabled = true;
        var merged = (JsonObject)_panel.DeepClone();
        foreach (var zoneOverride in Overrides)
        {
            if (zoneOverride.Matches(monitorId, monitorInstanceId, zoneNumber))
            {
                enabled = zoneOverride.Enabled ?? enabled;
                Overlay(merged, zoneOverride.Style);
            }
        }

        var resolved = new ResolvedPanelSettings(enabled, BuildStyle(merged, "panel"));
        _cache[key] = resolved;
        return resolved;
    }

    private static ZoneOverride ParseOverride(JsonNode? node, int index)
    {
        var entry = node as JsonObject ?? throw new SettingsException($"zones[{index}] must be an object.");
        string? monitor = null;
        int? zone = null;
        bool? enabled = null;
        var style = new JsonObject();

        foreach (var (name, value) in entry)
        {
            switch (name.ToLowerInvariant())
            {
                case "monitor":
                    monitor = Read<string?>(value, $"zones[{index}].monitor");
                    break;
                case "zone":
                    zone = Read<int?>(value, $"zones[{index}].zone");
                    if (zone < 1)
                    {
                        throw new SettingsException($"zones[{index}].zone: zone numbers start at 1.");
                    }

                    break;
                case "enabled":
                    enabled = Read<bool?>(value, $"zones[{index}].enabled");
                    break;
                default:
                    style[name] = value?.DeepClone();
                    break;
            }
        }

        return new ZoneOverride(monitor, zone, enabled, style);
    }

    private static void Overlay(JsonObject target, JsonObject source)
    {
        foreach (var (name, value) in source)
        {
            // Match keys case-insensitively so "Background" overrides "background".
            var existing = target.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                target.Remove(existing);
            }

            target[name] = value?.DeepClone();
        }
    }

    private static PanelStyle BuildStyle(JsonObject json, string context)
    {
        PanelStyle style;
        try
        {
            style = json.Deserialize<PanelStyle>(JsonOptions) ?? new PanelStyle();
        }
        catch (JsonException ex)
        {
            throw new SettingsException($"{context}: {Describe(ex)}", ex);
        }

        var problems = style.Validate().ToList();
        if (problems.Count > 0)
        {
            throw new SettingsException($"{context}: {string.Join("; ", problems)}");
        }

        style.Normalize();
        return style;
    }

    private static T Read<T>(JsonNode? value, string name)
    {
        try
        {
            return value is null ? default! : value.Deserialize<T>(JsonOptions)!;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new SettingsException($"{name}: expected {Describe(typeof(T))}.", ex);
        }
    }

    private static string Describe(JsonException ex)
    {
        // System.Text.Json's messages name .NET types; rewrite the common ones in settings-file terms.
        if (ex.Message.Contains("could not be mapped", StringComparison.Ordinal) && ex.Path is { } path)
        {
            return $"unknown setting \"{path.TrimStart('$', '.')}\"";
        }

        return ex.Path is { Length: > 1 } p ? $"{p.TrimStart('$', '.')} has an invalid value" : ex.Message;
    }

    private static string Describe(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(bool) => "true or false",
        var t when t == typeof(int) => "a whole number",
        var t when t == typeof(string) => "text in quotes (or null)",
        var t => t.Name,
    };
}
