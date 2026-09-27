using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class PowerWidgetSettingsTests
{
    [Fact]
    public void Normalize_falls_back_to_status_layout()
    {
        var settings = PowerWidgetSettings.Normalize(new PowerWidgetSettings
        {
            Layout = "nope",
            HiddenDeviceIds = ["  ", "case", "case", "keyboard"]
        });
        Assert.Equal(PowerLayouts.Status, settings.Layout);
        Assert.Equal(["case", "keyboard"], settings.HiddenDeviceIds);
        Assert.Equal("百分比圆环", PowerFormat.LayoutTitle(PowerLayouts.PercentageRing));
        Assert.Equal("仅图标", PowerFormat.LayoutTitle(PowerLayouts.IconOnly));
        Assert.Equal("状态", PowerFormat.LayoutTitle("nope"));
    }

    [Fact]
    public void Visible_on_dock_hides_selected_devices()
    {
        var devices = new[]
        {
            Device("system", "PC", 76, false),
            new PowerDeviceSnapshot
            {
                Id = "buds",
                Name = "AirPods",
                Kind = PowerDeviceKind.Headphones,
                Percent = 85
            },
            new PowerDeviceSnapshot
            {
                Id = "case",
                Name = "AirPods Case",
                Kind = PowerDeviceKind.Case,
                Percent = 32,
                Charging = true
            }
        };
        var hidden = new PowerWidgetSettings { HiddenDeviceIds = ["case"] };
        var visible = PowerWidgetSettings.VisibleOnDock(devices, hidden);
        Assert.Equal(2, visible.Count);
        Assert.DoesNotContain(visible, device => device.Id == "case");
        Assert.True(PowerWidgetSettings.Default.IsVisibleOnDock("case"));
        Assert.False(hidden.IsVisibleOnDock("case"));
        Assert.Equal("充电中", PowerFormat.Status(devices[2]));
        Assert.Equal("使用电池", PowerFormat.Status(devices[0]));
        Assert.Equal("76%", PowerFormat.Percent(76));
        Assert.Equal("—", PowerFormat.Percent(null));
        Assert.Equal("未检测到电池或已连接的蓝牙设备", PowerFormat.NoDevices);
        Assert.Equal("未检测到本机电池", PowerFormat.NoBattery);
        var unknown = new PowerDeviceSnapshot
        {
            Id = "system",
            Name = "PC",
            Kind = PowerDeviceKind.Laptop
        };
        Assert.False(PowerFormat.HasLevel(unknown));
        Assert.Empty(PowerWidgetSettings.VisibleOnDock([devices[0] with { Percent = null }], null));
        Assert.Equal(["buds"], PowerFormat.WithLevel([unknown, devices[1]]).Select(device => device.Id));
    }

    [Fact]
    public void Kind_from_name_classifies_common_peripherals()
    {
        Assert.Equal(PowerDeviceKind.Headphones, PowerFormat.KindFromName("AirPods Pro"));
        Assert.Equal(PowerDeviceKind.Headphones, PowerFormat.KindFromName("vivo TWS A2"));
        Assert.Equal(PowerDeviceKind.Case, PowerFormat.KindFromName("AirPods Case"));
        Assert.Equal(PowerDeviceKind.Keyboard, PowerFormat.KindFromName("Magic Keyboard"));
        Assert.Equal(PowerDeviceKind.Mouse, PowerFormat.KindFromName("无线鼠标"));
        Assert.Equal(PowerDeviceKind.Laptop, PowerFormat.KindFromName("MacBook Pro"));
        Assert.Equal(PowerDeviceKind.Other, PowerFormat.KindFromName("Speaker"));
        Assert.Equal("vivo TWS A2", PowerFormat.CleanDeviceName("vivo TWS A2 Hands-Free AG"));
        Assert.Equal("vivo TWS A2", PowerFormat.CleanDeviceName("vivo TWS A2 Hands-Free"));
    }

    [Fact]
    public void Disconnected_bluetooth_peripherals_are_hidden()
    {
        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.False(PowerFormat.IsLivePeripheral(false, "D498B9D4B955", connected));
        Assert.True(PowerFormat.IsLivePeripheral(true, "D498B9D4B955", connected));
        Assert.True(PowerFormat.IsLivePeripheral(null, null, connected));
        connected.Add("D498B9D4B955");
        Assert.True(PowerFormat.IsLivePeripheral(false, "D498B9D4B955", connected));
        Assert.Equal("D498B9D4B955", PowerFormat.NormalizeBluetoothAddress("d4:98:b9:d4:b9:55"));
        Assert.Equal("E30BB84382A7", PowerFormat.NormalizeBluetoothAddress("e30bb84382a7"));
        Assert.Null(PowerFormat.NormalizeBluetoothAddress("not-a-mac"));
    }

    [Fact]
    public void Power_poll_bursts_after_a_device_appears()
    {
        var now = DateTimeOffset.Parse("2026-09-14T13:00:00Z");
        Assert.Equal(PowerPoll.Idle, PowerPoll.Interval(now, now));
        Assert.Equal(PowerPoll.Burst, PowerPoll.Interval(now, PowerPoll.ExtendBurst(now)));

        var before = new PowerSnapshot
        {
            Timestamp = now,
            HasBattery = true,
            Devices = [Device("system", "PC", 76, false)]
        };
        var after = before with
        {
            Timestamp = now.AddSeconds(1),
            Devices =
            [
                Device("system", "PC", 76, false),
                new PowerDeviceSnapshot
                {
                    Id = "buds",
                    Name = "vivo TWS A2",
                    Kind = PowerDeviceKind.Headphones,
                    Percent = 70
                }
            ]
        };
        Assert.True(PowerPoll.PresenceChanged(before.Devices, after.Devices));
        Assert.False(PowerPoll.SameReading(before, after));
        Assert.True(PowerPoll.SameReading(after, after with { Timestamp = now.AddSeconds(4) }));
        Assert.False(PowerPoll.SameReading(after, after with
        {
            Devices =
            [
                after.Devices[0],
                after.Devices[1] with { Percent = 64 }
            ]
        }));
    }

    private static PowerDeviceSnapshot Device(string id, string name, int percent, bool charging) => new()
    {
        Id = id,
        Name = name,
        Kind = PowerDeviceKind.Laptop,
        Percent = percent,
        Charging = charging
    };
}
