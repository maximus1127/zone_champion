using Microsoft.Win32;

namespace ZoneChampion.Settings;

/// <summary>Registers Zone Champion to launch at sign-in (HKCU Run key).</summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZoneChampion";

    public static void Apply(bool enabled)
    {
#if DEBUG
        // Development builds run from bin\Debug; don't register those for startup.
        _ = enabled;
#else
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            string command = $"\"{Environment.ProcessPath}\"";
            if (enabled)
            {
                if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(ValueName, command);
                    Log.Info($"Registered for startup: {command}");
                }
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
                Log.Info("Removed from startup");
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            Log.Warn($"Couldn't update startup registration: {ex.Message}");
        }
#endif
    }
}
