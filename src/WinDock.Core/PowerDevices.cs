namespace WinDock.Core;

public enum PowerDeviceKind
{
    Laptop,
    Headphones,
    Case,
    Keyboard,
    Mouse,
    Phone,
    Other
}

public sealed record PowerDeviceSnapshot
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public PowerDeviceKind Kind { get; init; }

    public int? Percent { get; init; }

    public bool Charging { get; init; }
}

public static class PowerLayouts
{
    public const string Status = "status";
    public const string PercentageRing = "percentageRing";
    public const string IconOnly = "iconOnly";

    public static readonly IReadOnlyList<string> All = [Status, PercentageRing, IconOnly];

    public static string Normalize(string? layout) => layout switch
    {
        PercentageRing => PercentageRing,
        IconOnly => IconOnly,
        _ => Status
    };
}

public sealed record PowerWidgetSettings
{
    public string Layout { get; init; } = PowerLayouts.Status;

    public IReadOnlyList<string> HiddenDeviceIds { get; init; } = [];

    public static PowerWidgetSettings Default { get; } = new();

    public static PowerWidgetSettings Normalize(PowerWidgetSettings? settings)
    {
        var value = settings ?? Default;
        var hidden = value.HiddenDeviceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return value with
        {
            Layout = PowerLayouts.Normalize(value.Layout),
            HiddenDeviceIds = hidden
        };
    }

    public static IReadOnlyList<PowerDeviceSnapshot> VisibleOnDock(
        IReadOnlyList<PowerDeviceSnapshot> devices,
        PowerWidgetSettings? settings)
    {
        var shown = PowerFormat.WithLevel(devices);
        var hidden = Normalize(settings).HiddenDeviceIds.ToHashSet(StringComparer.Ordinal);
        if (hidden.Count == 0)
        {
            return shown;
        }

        return shown.Where(device => !hidden.Contains(device.Id)).ToArray();
    }

    public bool IsVisibleOnDock(string deviceId) =>
        !HiddenDeviceIds.Contains(deviceId, StringComparer.Ordinal);
}

public static class PowerFormat
{
    public const string SystemId = "system";

    public static string Status(PowerDeviceSnapshot device) =>
        device.Charging ? UiLocale.T("充电中", "Charging") : UiLocale.T("使用电池", "On battery");

    public static string NoDevices => UiLocale.T("未检测到电池或已连接的蓝牙设备", "No battery or connected Bluetooth devices");

    public static string NoBattery => UiLocale.T("未检测到本机电池", "No internal battery detected");

    public static string Percent(int? percent) => percent is int value ? $"{value}%" : "—";

    public static bool HasLevel(PowerDeviceSnapshot device) => device.Percent is >= 0 and <= 100;

    public static IReadOnlyList<PowerDeviceSnapshot> WithLevel(
        IReadOnlyList<PowerDeviceSnapshot> devices) =>
        devices.Where(HasLevel).ToArray();

    public static string LayoutTitle(string layout) => PowerLayouts.Normalize(layout) switch
    {
        PowerLayouts.PercentageRing => UiLocale.T("百分比圆环", "Percentage ring"),
        PowerLayouts.IconOnly => UiLocale.T("仅图标", "Icon only"),
        _ => UiLocale.T("状态", "Status")
    };

    public static string? NormalizeBluetoothAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = new char[12];
        var count = 0;
        foreach (var c in value)
        {
            if (!Uri.IsHexDigit(c))
            {
                continue;
            }

            if (count == 12)
            {
                return null;
            }

            hex[count++] = char.ToUpperInvariant(c);
        }

        return count == 12 ? new string(hex) : null;
    }

    public static bool IsLivePeripheral(
        bool? nodeConnected,
        string? bluetoothAddress,
        IReadOnlySet<string> connectedAddresses)
    {
        if (nodeConnected == true)
        {
            return true;
        }

        if (bluetoothAddress is { Length: > 0 } && connectedAddresses.Contains(bluetoothAddress))
        {
            return true;
        }

        return nodeConnected != false;
    }

    public static string CleanDeviceName(string name)
    {
        var text = name.Trim();
        foreach (var suffix in new[]
                 {
                     " Hands-Free AG",
                     " Hands-Free",
                     " Avrcp Transport",
                     " AVRCP Transport",
                     " Stereo"
                 })
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^suffix.Length].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(text) ? name.Trim() : text;
    }

    public static PowerDeviceKind KindFromName(string name)
    {
        var text = name.ToLowerInvariant();
        if (ContainsAny(text, "airpod", "buds", "headphone", "headset", "earphone", "earbud", "tws", "耳机"))
        {
            return ContainsAny(text, "case", "盒", "仓") ? PowerDeviceKind.Case : PowerDeviceKind.Headphones;
        }

        if (ContainsAny(text, "case", "盒", "仓"))
        {
            return PowerDeviceKind.Case;
        }

        if (ContainsAny(text, "keyboard", "键盘"))
        {
            return PowerDeviceKind.Keyboard;
        }

        if (ContainsAny(text, "mouse", "鼠标"))
        {
            return PowerDeviceKind.Mouse;
        }

        if (ContainsAny(text, "phone", "iphone", "pixel", "手机"))
        {
            return PowerDeviceKind.Phone;
        }

        if (ContainsAny(text, "laptop", "macbook", "surface", "notebook", "笔记本"))
        {
            return PowerDeviceKind.Laptop;
        }

        return PowerDeviceKind.Other;
    }

    private static bool ContainsAny(string text, params string[] parts) =>
        parts.Any(part => text.Contains(part, StringComparison.OrdinalIgnoreCase));
}

public static class PowerPoll
{
    public static readonly TimeSpan Idle = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Burst = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan BurstFor = TimeSpan.FromSeconds(12);

    public static TimeSpan Interval(DateTimeOffset now, DateTimeOffset burstUntil) =>
        now < burstUntil ? Burst : Idle;

    public static DateTimeOffset ExtendBurst(DateTimeOffset now) => now + BurstFor;

    public static bool PresenceChanged(
        IReadOnlyList<PowerDeviceSnapshot> before,
        IReadOnlyList<PowerDeviceSnapshot> after)
    {
        var previous = before.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var next = after.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !previous.SetEquals(next);
    }

    public static bool SameReading(PowerSnapshot left, PowerSnapshot right)
    {
        if (left.HasBattery != right.HasBattery
            || left.OnAcPower != right.OnAcPower
            || left.PercentRemaining != right.PercentRemaining
            || left.Devices.Count != right.Devices.Count)
        {
            return false;
        }

        foreach (var device in left.Devices)
        {
            var match = right.Devices.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, device.Id, StringComparison.OrdinalIgnoreCase));
            if (match is null || match.Percent != device.Percent || match.Charging != device.Charging)
            {
                return false;
            }
        }

        return true;
    }
}
