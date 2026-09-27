using System.Text;
using System.Text.Json;
using WinDock.Core;
using Xunit;

namespace WinDock.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Load_writes_factory_settings_when_file_is_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new WinDock.Infrastructure.JsonSettingsStore(
                Path.Combine(dir, "settings.json"),
                Path.Combine(dir, "settings.bak"));

            var loaded = store.Load();
            Assert.Equal(DefaultProfiles.GeneralId, loaded.ActiveProfileId);
            Assert.Null(store.LastLoadMessage);
            Assert.False(store.SkipNotesSweep);
            Assert.True(File.Exists(Path.Combine(dir, "settings.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_repairs_missing_general_profile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "activeProfileId": "deep-work",
              "profiles": [
                {
                  "id": "deep-work",
                  "name": "Deep Work",
                  "triggers": [ { "type": "focus", "active": true } ],
                  "dock": { "edge": "top", "monitor": "primary", "items": [] },
                  "widgets": [],
                  "actions": [],
                  "permissions": []
                }
              ]
            }
            """, Encoding.UTF8);

        var store = new WinDock.Infrastructure.JsonSettingsStore(path, Path.Combine(dir, "settings.bak"));
        var loaded = store.Load();
        Assert.Contains(loaded.Profiles, p => p.Id == DefaultProfiles.GeneralId);
        Assert.Equal(DockBackdrop.Mica, loaded.Ui.Backdrop);
    }

    [Fact]
    public void Newer_schema_does_not_overwrite_and_falls_back_to_factory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 99,
              "activeProfileId": "general",
              "profiles": []
            }
            """, Encoding.UTF8);

        var store = new WinDock.Infrastructure.JsonSettingsStore(path, Path.Combine(dir, "settings.bak"));
        try
        {
            var loaded = store.Load();
            Assert.Equal(DefaultProfiles.GeneralId, loaded.ActiveProfileId);
            Assert.Contains("更新的版本", store.LastLoadMessage);
            Assert.True(store.IsImmutable);
            Assert.True(store.SkipNotesSweep);
            Assert.Contains("\"schemaVersion\": 99", File.ReadAllText(path));
            store.Save(DefaultProfiles.CreateFactorySettings());
            Assert.Contains("\"schemaVersion\": 99", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_assigns_and_persists_missing_dock_item_ids()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "activeProfileId": "general",
              "profiles": [
                {
                  "id": "general",
                  "name": "日常",
                  "dock": {
                    "edge": "top",
                    "monitor": "primary",
                    "items": [ { "type": "widgetChip", "widgetId": "clock" } ]
                  },
                  "widgets": [ { "id": "clock", "visible": true } ]
                }
              ]
            }
            """, Encoding.UTF8);

        var store = new WinDock.Infrastructure.JsonSettingsStore(path, Path.Combine(dir, "settings.bak"));
        try
        {
            var loaded = store.Load();
            var clock = loaded.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
                .First(item => item.WidgetId == BuiltInWidgets.Clock);
            Assert.False(string.IsNullOrWhiteSpace(clock.Id));
            var roundTrip = store.Load();
            var again = roundTrip.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
                .First(item => item.WidgetId == BuiltInWidgets.Clock);
            Assert.Equal(clock.Id, again.Id);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Newer_schema_is_refused_on_save()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new WinDock.Infrastructure.JsonSettingsStore(
            Path.Combine(dir, "settings.json"),
            Path.Combine(dir, "settings.bak"));

        var settings = DefaultProfiles.CreateFactorySettings() with { SchemaVersion = 99 };
        try
        {
            Assert.Throws<InvalidOperationException>(() => store.Save(settings));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Json_round_trip_preserves_camel_case_enums()
    {
        var json = JsonSerializer.Serialize(
            DefaultProfiles.CreateFactorySettings(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });

        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"dockMode\":\"overlay\"", json);
        Assert.Contains("\"backdrop\":\"mica\"", json);
        Assert.Contains("\"islandTransparency\":50", json);
        Assert.Contains("\"type\":\"focus\"", json);
    }

    [Fact]
    public void Hidden_notes_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            var store = new WinDock.Infrastructure.JsonSettingsStore(path, Path.Combine(dir, "settings.bak"));
            var settings = SettingsEditor.AddWidget(
                DefaultProfiles.CreateFactorySettings(),
                DefaultProfiles.GeneralId,
                BuiltInWidgets.Notes);
            var id = settings.Profiles.First(p => p.Id == DefaultProfiles.GeneralId).Dock.Items
                .First(item => item.WidgetId == BuiltInWidgets.Notes).Id;
            store.Save(SettingsEditor.HideDockItem(settings, DefaultProfiles.GeneralId, id));

            var loaded = store.Load();
            var general = loaded.Profiles.First(p => p.Id == DefaultProfiles.GeneralId);
            Assert.Equal(id, Assert.Single(SettingsEditor.HiddenNotes(general)).Id);
            Assert.DoesNotContain(general.Dock.Items, item => item.Id == id);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AtomicFile_writes_safely_and_cleans_up_temp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WinDock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "atomic-test.json");
            WinDock.Infrastructure.AtomicFile.WriteAllText(path, "{\"test\": 1}");
            Assert.True(File.Exists(path));
            Assert.Equal("{\"test\": 1}", File.ReadAllText(path));

            WinDock.Infrastructure.AtomicFile.WriteAllText(path, "{\"test\": 2}");
            Assert.Equal("{\"test\": 2}", File.ReadAllText(path));

            var tmpFiles = Directory.GetFiles(dir, "*.tmp");
            Assert.Empty(tmpFiles);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
