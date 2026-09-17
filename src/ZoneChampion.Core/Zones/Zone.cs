using ZoneChampion.Core.Geometry;

namespace ZoneChampion.Core.Zones;

/// <summary>A physical display as seen by Win32, plus the ids FancyZones uses to identify it.</summary>
/// <param name="DeviceName">GDI device name, e.g. <c>\\.\DISPLAY1</c>.</param>
/// <param name="DeviceId">Monitor hardware id, e.g. <c>RKU0550</c> (FancyZones' "monitor").</param>
/// <param name="InstanceId">Device instance, e.g. <c>5&amp;2f3cbba3&amp;0&amp;UID4356</c> (FancyZones' "monitor-instance").</param>
/// <param name="MonitorNumber">The digits of the display name, e.g. 1 for <c>\\.\DISPLAY1</c> (FancyZones' "monitor-number").</param>
public sealed record DisplayMonitor(
    nint Handle,
    string DeviceName,
    string DeviceId,
    string InstanceId,
    int MonitorNumber,
    RectI Bounds,
    RectI WorkArea,
    int Dpi)
{
    public double Scale => Dpi / 96.0;
}

/// <summary>Stable identity of a zone: the monitor it lives on and its FancyZones zone index.</summary>
public readonly record struct ZoneKey(string MonitorInstanceId, int Index)
{
    /// <summary>1-based number, matching what the FancyZones editor displays.</summary>
    public int Number => Index + 1;

    public override string ToString() => $"{MonitorInstanceId} zone {Number}";
}

/// <summary>A zone rectangle in physical screen coordinates.</summary>
public sealed record Zone(ZoneKey Key, RectI Bounds, DisplayMonitor Monitor);
