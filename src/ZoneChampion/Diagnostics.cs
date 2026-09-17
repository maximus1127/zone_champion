using System.IO;
using System.Text;
using ZoneChampion.Core.FancyZones;
using ZoneChampion.Core.Settings;
using ZoneChampion.Core.Zones;
using ZoneChampion.Interop;
using ZoneChampion.Tracking;

namespace ZoneChampion;

/// <summary>
/// <c>ZoneChampion.exe --diagnose [file]</c>: writes what the app sees (monitors, zones, windows and their zones)
/// to a text file without showing any panels. Useful when a panel shows up in the wrong place.
/// </summary>
internal static class Diagnostics
{
    public static string Write(string? path)
    {
        path ??= Path.Combine(Log.FolderPath, "diagnostics.txt");
        var report = new StringBuilder();
        report.AppendLine($"Zone Champion diagnostics, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var settingsPath = Path.Combine(Log.FolderPath, "settings.json");
        var settings = SettingsDocument.Default;
        report.AppendLine().AppendLine($"Settings: {settingsPath}");
        try
        {
            if (File.Exists(settingsPath))
            {
                settings = SettingsDocument.Parse(File.ReadAllText(settingsPath));
                report.AppendLine("  OK");
            }
            else
            {
                report.AppendLine("  (not created yet; defaults)");
            }
        }
        catch (SettingsException ex)
        {
            report.AppendLine($"  ERROR: {ex.Message}");
        }

        var folder = settings.FancyZonesDataFolder is { Length: > 0 } custom ? Environment.ExpandEnvironmentVariables(custom) : FancyZonesData.DefaultFolder;
        var monitors = Monitors.Enumerate();
        report.AppendLine().AppendLine("Monitors:");
        foreach (var monitor in monitors)
        {
            report.AppendLine($"  {monitor.DeviceName}  id={monitor.DeviceId}  instance={monitor.InstanceId}  number={monitor.MonitorNumber}  dpi={monitor.Dpi}");
            report.AppendLine($"    bounds {monitor.Bounds}  work area {monitor.WorkArea}");
        }

        var desktop = VirtualDesktops.Current();
        var lastUsed = VirtualDesktops.LastUsed(folder);
        report.AppendLine().AppendLine($"Virtual desktop: current {desktop:B}, FancyZones last used {lastUsed:B}");

        var data = FancyZonesData.Load(folder);
        report.AppendLine().AppendLine($"FancyZones data: {folder} ({(data.FolderFound ? "found" : "NOT FOUND")})");
        report.AppendLine($"  {data.AppliedLayouts.Count} applied, {data.CustomLayouts.Count} custom, {data.DefaultLayouts.Count} default layouts");
        foreach (var warning in data.Warnings)
        {
            report.AppendLine($"  warning: {warning}");
        }

        IReadOnlyList<Zone> zones = data.FolderFound ? ZoneResolver.Resolve(data, monitors, desktop, lastUsed) : [];
        report.AppendLine().AppendLine("Zones:");
        foreach (var monitor in monitors)
        {
            var layout = ZoneResolver.FindLayout(data, monitor, desktop, lastUsed);
            report.AppendLine($"  {monitor.DeviceId}: layout {layout.Type} ({layout.ZoneCount} zones, spacing {(layout.ShowSpacing ? layout.Spacing : 0)})");
            foreach (var zone in zones.Where(z => z.Monitor == monitor))
            {
                var resolved = settings.Resolve(monitor.DeviceId, monitor.InstanceId, zone.Key.Number);
                var edge = Core.Panels.PanelPlacement.ResolveEdge(resolved.Style.Edge, zone.Bounds, monitor.WorkArea);
                report.AppendLine($"    zone {zone.Key.Number}: {zone.Bounds}  panel {(resolved.Enabled ? edge.ToString().ToLowerInvariant() : "off")}");
            }
        }

        using (var tracker = new WindowTracker())
        {
            tracker.SetZones(zones, monitors);
            report.AppendLine().AppendLine("Windows (taskbar-style, current desktop):");
            foreach (var window in tracker.Windows.OrderBy(w => w.Zone?.Key.MonitorInstanceId).ThenBy(w => w.Zone?.Key.Index).ThenBy(w => w.Sequence))
            {
                var zone = window.Zone is { } z ? $"{z.Monitor.DeviceId} zone {z.Key.Number}" : "no zone";
                var state = window.IsMinimized ? " minimized" : window.IsMaximized ? " maximized" : "";
                report.AppendLine($"  [{zone}]{state}  {Path.GetFileName(window.ExePath)}  \"{window.Title}\"  {window.Bounds}");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, report.ToString());
        return path;
    }
}
