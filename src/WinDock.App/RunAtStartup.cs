using Microsoft.Win32;
using WinDock.Core;

namespace WinDock.App;

internal static class RunAtStartup
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return HasValue(key, ProductInfo.StartupValueName) ||
                   HasValue(key, ProductInfo.LegacyStartupValueName) ||
                   HasValue(key, ProductInfo.LegacyStartupValueName2);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Migrate()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            var legacy = (key.GetValue(ProductInfo.LegacyStartupValueName) as string) ??
                         (key.GetValue(ProductInfo.LegacyStartupValueName2) as string);
            var current = key.GetValue(ProductInfo.StartupValueName) as string;
            if (!string.IsNullOrWhiteSpace(legacy) && string.IsNullOrWhiteSpace(current))
            {
                var path = Environment.ProcessPath;
                key.SetValue(ProductInfo.StartupValueName, string.IsNullOrWhiteSpace(path) ? legacy : $"\"{path}\"");
            }

            key.DeleteValue(ProductInfo.LegacyStartupValueName, throwOnMissingValue: false);
            key.DeleteValue(ProductInfo.LegacyStartupValueName2, throwOnMissingValue: false);
        }
        catch (Exception)
        {
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (key is null)
        {
            return;
        }

        key.DeleteValue(ProductInfo.LegacyStartupValueName, throwOnMissingValue: false);
        key.DeleteValue(ProductInfo.LegacyStartupValueName2, throwOnMissingValue: false);
        if (!enabled)
        {
            key.DeleteValue(ProductInfo.StartupValueName, throwOnMissingValue: false);
            return;
        }

        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        key.SetValue(ProductInfo.StartupValueName, $"\"{path}\"");
    }

    private static bool HasValue(RegistryKey? key, string name) =>
        key?.GetValue(name) is string text && !string.IsNullOrWhiteSpace(text);
}
