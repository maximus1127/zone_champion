using System.Runtime.InteropServices;
using ZoneChampion.Core.Zones;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

internal static class Monitors
{
    private const int MDT_EFFECTIVE_DPI = 0;

    public static IReadOnlyList<DisplayMonitor> Enumerate()
    {
        var handles = new List<nint>();
        EnumDisplayMonitors(0, 0, (nint monitor, nint _, ref RECT _, nint _) =>
        {
            handles.Add(monitor);
            return true;
        }, 0);

        var monitors = new List<DisplayMonitor>();
        foreach (var handle in handles)
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(handle, ref info))
            {
                continue;
            }

            var (deviceId, instanceId, number) = Identify(info.szDevice);
            int dpi = GetDpiForMonitor(handle, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? (int)dpiX : 96;
            monitors.Add(new DisplayMonitor(handle, info.szDevice, deviceId, instanceId, number, info.rcMonitor.ToRectI(), info.rcWork.ToRectI(), dpi));
        }

        return monitors;
    }

    /// <summary>A string that changes whenever monitors are added, removed, moved, rescaled or their work area changes.</summary>
    public static string Signature(IEnumerable<DisplayMonitor> monitors) =>
        string.Join("|", monitors.Select(m => $"{m.Handle}:{m.InstanceId}:{m.Bounds}:{m.WorkArea}:{m.Dpi}"));

    /// <summary>
    /// Derives FancyZones' monitor ids from the display's device interface name, e.g.
    /// <c>\\?\DISPLAY#RKU0550#5&amp;2f3cbba3&amp;0&amp;UID4356#{e6f07b5f-...}</c> → ("RKU0550", "5&amp;2f3cbba3&amp;0&amp;UID4356").
    /// </summary>
    private static (string DeviceId, string InstanceId, int Number) Identify(string deviceName)
    {
        var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(deviceName, i, ref device, EDD_GET_DEVICE_INTERFACE_NAME); i++)
        {
            if ((device.StateFlags & DISPLAY_DEVICE_ACTIVE) != 0 && (device.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) == 0)
            {
                var parts = device.DeviceID.Split('#');
                int lastSlash = device.DeviceName.LastIndexOf('\\');
                int number = Digits(lastSlash > 0 ? device.DeviceName[..lastSlash] : device.DeviceName);
                return parts.Length >= 4
                    ? (parts[1], parts[2], number)
                    : (device.DeviceID, deviceName, number);
            }

            device.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        }

        // Same fallback as FancyZones: identify by display name.
        return (deviceName, deviceName, Digits(deviceName));
    }

    private static int Digits(string text) =>
        int.TryParse(new string(text.Where(char.IsAsciiDigit).ToArray()), out int value) ? value : 0;
}
