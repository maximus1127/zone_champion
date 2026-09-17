using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ZoneChampion.Interop;

/// <summary>Reads virtual desktop ids from the same places FancyZones does.</summary>
internal static class VirtualDesktops
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    public static Guid Current()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            if (key?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } current)
            {
                return new Guid(current);
            }

            int session = Process.GetCurrentProcess().SessionId;
            using var sessionKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{session}\VirtualDesktops");
            if (sessionKey?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } sessionCurrent)
            {
                return new Guid(sessionCurrent);
            }

            if (key?.GetValue("VirtualDesktopIDs") is byte[] { Length: >= 16 } ids)
            {
                return new Guid(ids.AsSpan(0, 16));
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Couldn't read the current virtual desktop: {ex.Message}");
        }

        return Guid.Empty;
    }

    /// <summary>FancyZones' last-used-virtual-desktop.json, used when the current desktop has no layout yet.</summary>
    public static Guid LastUsed(string fancyZonesFolder)
    {
        try
        {
            var path = Path.Combine(fancyZonesFolder, "last-used-virtual-desktop.json");
            if (!File.Exists(path))
            {
                return Guid.Empty;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("last-used-virtual-desktop", out var value)
                && Guid.TryParse(value.GetString(), out var guid)
                    ? guid
                    : Guid.Empty;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Guid.Empty;
        }
    }
}
