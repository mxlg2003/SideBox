using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class MonitorWidgetTests
{
    [Fact]
    public void Disk_settings_normalize_and_hide_volumes()
    {
        var settings = DiskWidgetSettings.Normalize(new DiskWidgetSettings
        {
            Main = "nope",
            HiddenVolumeIds = ["  ", @"C:\", @"C:\", @"D:\"]
        });
        Assert.Equal(DiskMains.UsedPercent, settings.Main);
        Assert.Equal([@"C:\", @"D:\"], settings.HiddenVolumeIds);
        Assert.Equal("剩余空间", DiskFormat.MainTitle(DiskMains.Free));
        Assert.Equal("已用百分比", DiskFormat.MainTitle("nope"));

        var volumes = new[]
        {
            new DiskVolumeSnapshot { Id = @"C:\", Name = @"C:\", Label = "Windows", TotalBytes = 100, FreeBytes = 40 },
            new DiskVolumeSnapshot { Id = @"D:\", Name = @"D:\", Label = "Data", TotalBytes = 200, FreeBytes = 50 }
        };
        var visible = DiskWidgetSettings.Visible(volumes, settings);
        Assert.Empty(visible);
        Assert.Equal("—", DiskFormat.DockValue(visible, settings));
        Assert.Equal("70%", DiskFormat.DockValue(volumes, DiskWidgetSettings.Default));
        Assert.Equal("C: · Windows", DiskFormat.VolumeTitle(volumes[0]));
    }

    [Fact]
    public void Gpu_and_process_settings_normalize()
    {
        var gpu = GpuWidgetSettings.Normalize(new GpuWidgetSettings
        {
            AdapterId = "  luid_1  ",
            ShowVram = true,
            ShowEngines = true,
            ChartInDock = true
        });
        Assert.Equal("luid_1", gpu.AdapterId);
        Assert.True(gpu.ShowVram);
        Assert.Equal("—", GpuFormat.Engine(null));
        Assert.Equal("12%", GpuFormat.Engine(12));

        var processes = ProcessWidgetSettings.Normalize(new ProcessWidgetSettings { Sort = "nope" });
        Assert.Equal(ProcessSorts.Cpu, processes.Sort);
        Assert.Equal("内存", ProcessFormat.SortTitle(ProcessSorts.Memory));
        var rows = new[]
        {
            new ProcessRow { Pid = 1, Name = "a", CpuPercent = 1, WorkingSetBytes = 50_000_000 },
            new ProcessRow { Pid = 2, Name = "b", CpuPercent = 9, WorkingSetBytes = 10_000_000 }
        };
        Assert.Equal("b", ProcessFormat.Rank(rows, ProcessSorts.Cpu)[0].Name);
        Assert.Equal("a", ProcessFormat.Rank(rows, ProcessSorts.Memory)[0].Name);
        Assert.Equal("9.0%", ProcessFormat.Cpu(9));
    }

    [Fact]
    public void Sys_activity_and_power_optional_flags_default_off()
    {
        var activity = SysActivitySettings.Normalize(new SysActivitySettings { Layout = SysActivityLayouts.Bars, ChartInDock = true });
        Assert.Equal(SysActivityLayouts.Bars, activity.Layout);
        Assert.True(activity.ChartInDock);
        Assert.False(SysActivitySettings.Default.ShowPerCore);
        Assert.False(SysActivitySettings.Default.ShowFrequency);
        Assert.Equal("3.60 GHz", SysActivityFormat.Frequency(3600, true));
        Assert.Equal("—", SysActivityFormat.Frequency(0, true));

        var power = PowerWidgetSettings.Normalize(new PowerWidgetSettings { ShowWatts = true, ShowHealth = true });
        Assert.True(power.ShowWatts);
        Assert.False(power.ShowRemaining);
        Assert.Equal("18.5 W", PowerFormat.Watts(18500));
        Assert.Equal("—", PowerFormat.Watts(null));
        Assert.Equal("2 小时 15 分钟", PowerFormat.Remaining(2 * 3600 + 15 * 60));
        Assert.Equal("—", PowerFormat.Remaining(-1));
        Assert.Equal("—", PowerFormat.Remaining(0));
        Assert.Equal("94%", PowerFormat.Health(94));
        Assert.False(NetworkWidgetSettings.Default.ShowWifiSignal);
    }

    [Fact]
    public void Settings_editor_round_trips_new_monitor_widgets()
    {
        var settings = DefaultProfiles.CreateFactorySettings();
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Disk);
        settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Gpu);
        if (ProductFeatures.ProcessesEnabled)
        {
            settings = SettingsEditor.AddWidget(settings, DefaultProfiles.GeneralId, BuiltInWidgets.Processes);
        }
        else
        {
            var general = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
            settings = SettingsEditor.ReplaceDockItems(settings, DefaultProfiles.GeneralId,
                general.Dock.Items.Append(new DockItem
                {
                    Id = "proc-item",
                    Type = DockItemType.WidgetChip,
                    WidgetId = BuiltInWidgets.Processes
                }).ToArray());
        }
        var diskId = ItemId(settings, BuiltInWidgets.Disk);
        var gpuId = ItemId(settings, BuiltInWidgets.Gpu);
        var processId = ItemId(settings, BuiltInWidgets.Processes);
        settings = SettingsEditor.WithDiskWidget(settings, DefaultProfiles.GeneralId, diskId, new DiskWidgetSettings
        {
            Main = DiskMains.Free,
            HiddenVolumeIds = [@"E:\"]
        });
        settings = SettingsEditor.WithGpuWidget(settings, DefaultProfiles.GeneralId, gpuId, new GpuWidgetSettings
        {
            ShowVram = true,
            AdapterId = "gpu-1"
        });
        settings = SettingsEditor.WithProcessWidget(settings, DefaultProfiles.GeneralId, processId, new ProcessWidgetSettings
        {
            Sort = ProcessSorts.Memory
        });
        var items = settings.Profiles.First(profile => profile.Id == DefaultProfiles.GeneralId).Dock.Items;
        Assert.Equal(DiskMains.Free, items.First(item => item.Id == diskId).Disk?.Main);
        Assert.True(items.First(item => item.Id == gpuId).Gpu?.ShowVram);
        Assert.Equal(ProcessSorts.Memory, items.First(item => item.Id == processId).Processes?.Sort);
    }

    [Fact]
    public void Gated_process_list_strips_rows_when_privacy_is_off()
    {
        var inner = new StubProcesses
        {
            Current = new ProcessListSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                Allowed = true,
                Rows = [new ProcessRow { Pid = 1, Name = "chrome", CpuPercent = 12, WorkingSetBytes = 1 }]
            }
        };
        var gate = new WinDock.Core.Permissions.PermissionGate(new PrivacySettings());
        var gated = new GatedProcessListProvider(gate, inner);
        Assert.False(gated.Current.Allowed);
        Assert.Empty(gated.Current.Rows);
        gate.UpdatePrivacy(new PrivacySettings { ProcessNames = true });
        Assert.True(gated.Current.Allowed);
        Assert.Equal("chrome", gated.Current.Rows[0].Name);
    }

    private static string ItemId(AppSettings settings, string widgetId) =>
        settings.Profiles.First(profile => profile.Id == DefaultProfiles.GeneralId).Dock.Items
            .First(item => item.WidgetId == widgetId).Id;

    private sealed class StubProcesses : IProcessListProvider
    {
        public ProcessListSnapshot Current { get; set; } = new() { Timestamp = DateTimeOffset.UtcNow };

        public event EventHandler<ProcessListSnapshot>? Updated
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void SetLive(bool live)
        {
        }
    }
}
