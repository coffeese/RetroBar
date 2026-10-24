using RetroBar.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

// Compile the production settings and selector directly. Only the settings path extension is
// replaced, so these tests cannot read or overwrite the user's real RetroBar settings.
internal static class TestPaths
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "RetroBar.Tests", Guid.NewGuid().ToString("N"));

    public static string InLocalAppData(this string name) => Path.Combine(Root, name);
}

internal static class Program
{
    private record Monitor(string DeviceName, bool Primary);
    private static readonly Monitor Primary = new(@"\\.\DISPLAY1", true);
    private static readonly Monitor Secondary = new(@"\\.\DISPLAY2", false);
    private static readonly Monitor Third = new(@"\\.\DISPLAY3", false);
    private static readonly Monitor[] Screens = { Secondary, Primary, Third };
    private static int _passed;
    private static int _failed;

    private static Monitor[] Select(Settings settings, params Monitor[] screens) =>
        MonitorSelection.Select(screens, settings.ShowMultiMon, settings.TargetMonitorDeviceName,
            screen => screen.DeviceName, screen => screen.Primary).ToArray();

    private static Settings Read(string json) => JsonSerializer.Deserialize<Settings>(json);

    private static void Check(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"FAIL {name}: {ex.Message}");
        }
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new Exception("Assertion failed");
    }

    private static void IsSingle(Settings settings, Monitor expected, params Monitor[] screens)
    {
        var selected = Select(settings, screens);
        Require(selected.Length == 1 && selected[0] == expected);
    }

    private static int Main()
    {
        Directory.CreateDirectory(TestPaths.Root);
        try
        {
            Check("Old default JSON selects Primary regardless of enumeration order", () =>
            {
                var settings = Read("{}");
                Require(settings.TargetMonitorDeviceName == "" && !settings.MigrationPerformed);
                IsSingle(settings, Primary, Screens);
            });
            Check("Old Primary JSON preserves task filtering and edge", () =>
            {
                var settings = Read("{\"ShowMultiMon\":false,\"MultiMonMode\":2,\"Edge\":0}");
                Require(settings.MultiMonMode == MultiMonOption.SameAsWindowAndPrimary && (int)settings.Edge == 0);
                IsSingle(settings, Primary, Screens);
            });
            Check("Old All JSON selects every screen and preserves MultiMonMode", () =>
            {
                var settings = Read("{\"ShowMultiMon\":true,\"MultiMonMode\":1}");
                Require(Select(settings, Screens).SequenceEqual(Screens));
                Require(settings.MultiMonMode == MultiMonOption.SameAsWindow);
            });
            Check("Specific selects exactly one non-primary monitor", () =>
                IsSingle(new Settings { TargetMonitorDeviceName = Secondary.DeviceName }, Secondary, Screens));
            Check("Specific may explicitly select Primary", () =>
                IsSingle(new Settings { TargetMonitorDeviceName = Primary.DeviceName }, Primary, Screens));
            Check("Device matching is case insensitive", () =>
                IsSingle(new Settings { TargetMonitorDeviceName = Secondary.DeviceName.ToLowerInvariant() }, Secondary, Screens));
            Check("All takes precedence without erasing a saved Specific target", () =>
            {
                var settings = new Settings { ShowMultiMon = true, TargetMonitorDeviceName = Secondary.DeviceName };
                Require(Select(settings, Screens).Length == Screens.Length);
                settings.ShowMultiMon = false;
                IsSingle(settings, Secondary, Screens);
            });
            Check("Missing target falls back without changing saved settings", () =>
            {
                var settings = new Settings { TargetMonitorDeviceName = Secondary.DeviceName };
                string before = JsonSerializer.Serialize(settings);
                IsSingle(settings, Primary, Primary, Third);
                Require(JsonSerializer.Serialize(settings) == before);
            });
            Check("Reconnect returns to the saved target after enumeration order changes", () =>
            {
                var settings = new Settings { TargetMonitorDeviceName = Secondary.DeviceName };
                IsSingle(settings, Primary, Primary);
                IsSingle(settings, Secondary, Third, Primary, Secondary);
            });
            Check("Fallback follows a changed primary monitor", () =>
            {
                var newPrimary = Third with { Primary = true };
                IsSingle(new Settings { TargetMonitorDeviceName = "missing" }, newPrimary,
                    Primary with { Primary = false }, newPrimary);
            });
            Check("Reassigned device name safely falls back", () =>
                IsSingle(new Settings { TargetMonitorDeviceName = Secondary.DeviceName }, Primary,
                    Secondary with { DeviceName = "changed" }, Primary));
            Check("Null JSON target is normalized to Primary", () =>
            {
                var settings = Read("{\"TargetMonitorDeviceName\":null}");
                Require(settings.TargetMonitorDeviceName == "");
                IsSingle(settings, Primary, Screens);
                settings.TargetMonitorDeviceName = Secondary.DeviceName;
                settings.TargetMonitorDeviceName = null;
                IsSingle(settings, Primary, Screens);
            });
            Check("SettingsManager loads a legacy file without rewriting it", () =>
            {
                string path = Path.Combine(TestPaths.Root, "legacy.json");
                const string oldJson = "{\"ShowMultiMon\":true,\"MultiMonMode\":2}";
                File.WriteAllText(path, oldJson);
                var manager = new SettingsManager<Settings>(path, new Settings());
                Require(manager.Settings.ShowMultiMon && manager.Settings.TargetMonitorDeviceName == "");
                Require(File.ReadAllText(path) == oldJson);
            });
            Check("SettingsManager persists a disconnected target and the existing settings", () =>
            {
                string path = Path.Combine(TestPaths.Root, "roundtrip.json");
                var settings = new Settings
                {
                    TargetMonitorDeviceName = Secondary.DeviceName,
                    MultiMonMode = MultiMonOption.SameAsWindow,
                    Edge = ManagedShell.AppBar.AppBarEdge.Right
                };
                IsSingle(settings, Primary, Primary);
                var manager = new SettingsManager<Settings>(path, new Settings());
                manager.Settings = settings;
                var reloaded = new SettingsManager<Settings>(path, new Settings()).Settings;
                Require(!reloaded.ShowMultiMon && reloaded.TargetMonitorDeviceName == Secondary.DeviceName);
                Require(reloaded.MultiMonMode == settings.MultiMonMode && reloaded.Edge == settings.Edge);
                IsSingle(reloaded, Secondary, Screens);
            });
            Check("Settings changes notify once and automatically persist", () =>
            {
                var settings = Settings.Instance;
                int events = 0;
                settings.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Settings.TargetMonitorDeviceName)) events++; };
                settings.TargetMonitorDeviceName = Secondary.DeviceName;
                settings.TargetMonitorDeviceName = Secondary.DeviceName;
                Require(events == 1);
                var reloaded = Read(File.ReadAllText("settings.json".InLocalAppData()));
                Require(reloaded.TargetMonitorDeviceName == Secondary.DeviceName);
            });
            Check("Old JSON defaults Start menu to the clicked taskbar", () =>
            {
                var settings = Read("{\"ShowMultiMon\":true}");
                Require(settings.StartMenuMonitorDeviceName == "" && !settings.MigrationPerformed);
            });
            Check("Start menu selects a different monitor independently of All taskbars", () =>
            {
                var settings = new Settings
                {
                    ShowMultiMon = true,
                    TargetMonitorDeviceName = Secondary.DeviceName,
                    StartMenuMonitorDeviceName = Third.DeviceName
                };
                var startScreen = MonitorSelection.Select(Screens, false, settings.StartMenuMonitorDeviceName,
                    screen => screen.DeviceName, screen => screen.Primary);
                Require(startScreen.Count == 1 && startScreen[0] == Third);
                Require(Select(settings, Screens).Length == Screens.Length);
                Require(settings.TargetMonitorDeviceName == Secondary.DeviceName);
            });
            Check("Disconnected Start menu monitor falls back and returns on reconnect", () =>
            {
                var settings = new Settings { StartMenuMonitorDeviceName = Third.DeviceName };
                var disconnected = MonitorSelection.Select(new[] { Primary, Secondary }, false,
                    settings.StartMenuMonitorDeviceName, screen => screen.DeviceName, screen => screen.Primary);
                Require(disconnected.Count == 1 && disconnected[0] == Primary);
                Require(settings.StartMenuMonitorDeviceName == Third.DeviceName);
                var reconnected = MonitorSelection.Select(Screens, false, settings.StartMenuMonitorDeviceName,
                    screen => screen.DeviceName, screen => screen.Primary);
                Require(reconnected.Count == 1 && reconnected[0] == Third);
            });
            Check("Start menu and taskbar targets persist independently", () =>
            {
                string path = Path.Combine(TestPaths.Root, "start-menu.json");
                var settings = new Settings
                {
                    TargetMonitorDeviceName = Secondary.DeviceName,
                    StartMenuMonitorDeviceName = Third.DeviceName
                };
                var manager = new SettingsManager<Settings>(path, new Settings());
                manager.Settings = settings;
                var reloaded = new SettingsManager<Settings>(path, new Settings()).Settings;
                Require(reloaded.TargetMonitorDeviceName == Secondary.DeviceName &&
                    reloaded.StartMenuMonitorDeviceName == Third.DeviceName);
                IsSingle(reloaded, Secondary, Screens);
                settings.StartMenuMonitorDeviceName = "";
                manager.Settings = settings;
                reloaded = new SettingsManager<Settings>(path, new Settings()).Settings;
                Require(reloaded.StartMenuMonitorDeviceName == "" && reloaded.TargetMonitorDeviceName == Secondary.DeviceName);
            });
            Check("Null Start menu target safely restores the default", () =>
            {
                var settings = Read("{\"StartMenuMonitorDeviceName\":null}");
                Require(settings.StartMenuMonitorDeviceName == "");
                settings.StartMenuMonitorDeviceName = Third.DeviceName;
                settings.StartMenuMonitorDeviceName = null;
                Require(settings.StartMenuMonitorDeviceName == "");
            });
            Check("Start menu target changes notify once and automatically persist", () =>
            {
                var settings = Settings.Instance;
                int events = 0;
                settings.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Settings.StartMenuMonitorDeviceName)) events++; };
                settings.StartMenuMonitorDeviceName = Third.DeviceName;
                settings.StartMenuMonitorDeviceName = Third.DeviceName;
                Require(events == 1);
                var reloaded = Read(File.ReadAllText("settings.json".InLocalAppData()));
                Require(reloaded.StartMenuMonitorDeviceName == Third.DeviceName);
            });
        }
        finally
        {
            // Delete only this run's private, randomly named test directory.
            Directory.Delete(TestPaths.Root, true);
        }
        Console.WriteLine($"{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }
}
