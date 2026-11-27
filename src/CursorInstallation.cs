using Microsoft.Win32;

namespace CodexTaskbar;

internal static class CursorInstallation
{
    internal static bool IsInstalled()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (File.Exists(Path.Combine(local, "Programs", "Cursor", "Cursor.exe"))) return true;
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            if (File.Exists(Path.Combine(Environment.GetFolderPath(folder), "Cursor", "Cursor.exe"))) return true;

        // Custom installer locations can register either an app path or a URL handler.
        foreach (var hive in new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" })
        {
            foreach (var key in new[] { @"Software\Microsoft\Windows\CurrentVersion\App Paths\Cursor.exe", @"Software\Classes\cursor\shell\open\command" })
            {
                try
                {
                    if (ContainsExecutable(Registry.GetValue(hive + "\\" + key, "", null) as string)) return true;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
            }
        }
        // Installers can omit the URL handler and use only their uninstall registration.
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var key in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
        {
            try
            {
                using var entries = hive.OpenSubKey(key);
                if (entries is null) continue;
                foreach (var name in entries.GetSubKeyNames())
                {
                    using var entry = entries.OpenSubKey(name);
                    if (entry?.GetValue("DisplayName") is not string label ||
                        !(label.Equals("Cursor", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Cursor (", StringComparison.OrdinalIgnoreCase))) continue;
                    if (entry.GetValue("InstallLocation") is string location && Path.IsPathFullyQualified(location) &&
                        File.Exists(Path.Combine(location, "Cursor.exe"))) return true;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        }
        return false;
    }

    internal static bool ContainsExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        var value = Environment.ExpandEnvironmentVariables(command.Trim());
        // Allow quoted protocol commands with arguments and unquoted App Paths with spaces.
        var path = value.StartsWith('"') ? value.Split('"').ElementAtOrDefault(1) : value;
        return path is not null && Path.IsPathFullyQualified(path) &&
            string.Equals(Path.GetFileName(path), "Cursor.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
    }
}
