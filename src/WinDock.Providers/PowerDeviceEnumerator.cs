using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Power;
using WinDock.Core;

namespace WinDock.Providers;

internal static class PowerDeviceEnumerator
{
    private const string BatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    private static readonly string[] BatteryHints =
    [
        BatteryProperty,
        "System.Devices.BatteryLife",
        "System.Devices.Aep.BatteryLife"
    ];

    public static IReadOnlyList<PowerDeviceSnapshot> TryRead()
    {
        var devices = new List<PowerDeviceSnapshot>();
        try
        {
            foreach (var device in PnpBluetoothBatteries.Read())
            {
                Add(devices, device);
            }
        }
        catch (Exception)
        {
            // PnP is the source of truth for headphone battery; keep whatever we got.
        }

        return devices;
    }

    public static async Task<IReadOnlyList<PowerDeviceSnapshot>> ReadAsync()
    {
        var devices = TryRead().ToList();
        try
        {
            await AddWinRtBatteriesAsync(devices).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Optional.
        }

        return devices;
    }

    private static async Task AddWinRtBatteriesAsync(List<PowerDeviceSnapshot> devices)
    {
        try
        {
            var infos = await DeviceInformation.FindAllAsync(Battery.GetDeviceSelector());
            foreach (var info in infos)
            {
                if (info is null || LooksLikeSystemBattery(info.Id, info.Name))
                {
                    continue;
                }

                try
                {
                    var battery = await Battery.FromIdAsync(info.Id);
                    var report = battery.GetReport();
                    var percent = PercentFromReport(report);
                    if (percent is null)
                    {
                        continue;
                    }

                    Add(devices, new PowerDeviceSnapshot
                    {
                        Id = info.Id,
                        Name = DisplayName(info.Name),
                        Kind = PowerFormat.KindFromName(info.Name),
                        Percent = percent,
                        Charging = report.Status == Windows.System.Power.BatteryStatus.Charging
                    });
                }
                catch (Exception)
                {
                    // Skip devices Windows will not open.
                }
            }
        }
        catch (Exception)
        {
            // WinRT battery enumeration is best-effort.
        }
    }

    private static async Task AddPairedAsync(List<PowerDeviceSnapshot> devices, string selector)
    {
        foreach (var properties in PropertySets())
        {
            try
            {
                var infos = await DeviceInformation.FindAllAsync(selector, properties);
                foreach (var info in infos)
                {
                    if (info is null || LooksLikeSystemBattery(info.Id, info.Name))
                    {
                        continue;
                    }

                    var percent = PercentFromProperties(info);
                    if (percent is null)
                    {
                        continue;
                    }

                    Add(devices, new PowerDeviceSnapshot
                    {
                        Id = info.Id,
                        Name = DisplayName(info.Name),
                        Kind = PowerFormat.KindFromName(info.Name),
                        Percent = percent,
                        Charging = IsCharging(info)
                    });
                }

                return;
            }
            catch (Exception)
            {
                // Retry with fewer requested properties.
            }
        }
    }

    private static IEnumerable<string[]> PropertySets()
    {
        yield return BatteryHints;
        yield return [BatteryProperty];
        yield return [];
    }

    private static void Add(List<PowerDeviceSnapshot> devices, PowerDeviceSnapshot device)
    {
        if (string.IsNullOrWhiteSpace(device.Id) || string.IsNullOrWhiteSpace(device.Name))
        {
            return;
        }

        if (devices.Any(existing => string.Equals(existing.Id, device.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (devices.Any(existing =>
                string.Equals(existing.Name, device.Name, StringComparison.OrdinalIgnoreCase)
                && existing.Percent == device.Percent))
        {
            return;
        }

        devices.Add(device);
    }

    private static int? PercentFromReport(BatteryReport report)
    {
        return Percent(report.RemainingCapacityInMilliwattHours, report.FullChargeCapacityInMilliwattHours)
               ?? Percent(report.RemainingCapacityInMilliwattHours, report.DesignCapacityInMilliwattHours);
    }

    private static int? Percent(int? remaining, int? full)
    {
        if (remaining is not int left || full is not int capacity || capacity <= 0)
        {
            return null;
        }

        return Math.Clamp((int)Math.Round(left * 100.0 / capacity), 0, 100);
    }

    private static int? PercentFromProperties(DeviceInformation info)
    {
        foreach (var key in BatteryHints)
        {
            if (!info.Properties.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            try
            {
                var percent = Convert.ToInt32(value);
                if (percent is >= 0 and <= 100)
                {
                    return percent;
                }
            }
            catch (Exception)
            {
                // Keep scanning other keys.
            }
        }

        return null;
    }

    private static bool IsCharging(DeviceInformation info)
    {
        if (!info.Properties.TryGetValue("System.Devices.Aep.IsCharging", out var value) || value is null)
        {
            return false;
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool LooksLikeSystemBattery(string id, string name)
    {
        return Contains(id, "AggregateBattery", @"ACPI\PNP0C0A", "ACPI_HAL")
               || Contains(name, "ACPI", "系统电池", "System Battery");
    }

    private static bool Contains(string text, params string[] parts) =>
        parts.Any(part => text.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static string DisplayName(string name) =>
        string.IsNullOrWhiteSpace(name) ? UiText.Device : PowerFormat.CleanDeviceName(name);
}
