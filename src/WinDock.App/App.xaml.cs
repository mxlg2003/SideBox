using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinDock.Core;
using WinDock.Infrastructure;

namespace WinDock.App;

public partial class App : Application
{
    private DockSession? _session;
    private NativeTrayIcon? _tray;
    private ContextMenu? _trayMenu;
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, ProductInfo.MutexName, out var created);
        if (!created)
        {
            MessageBox.Show(UiText.AlreadyRunning, ProductInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            AppLog.Write("App.OnStartup starting...");
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                AppLog.Write($"AppDomain unhandled exception: {args.ExceptionObject}");

            DispatcherUnhandledException += (_, args) =>
            {
                AppLog.Write("Dispatcher unhandled exception.", args.Exception);
                args.Handled = true;
                _tray?.ShowBalloon(ProductInfo.Name, UiText.LoggedToFile, isWarning: true);
            };
            UserAlerts.Raised += OnAlert;
            AppIcon.AttachToWindows();
            RunAtStartup.Migrate();
            AppLog.Write("Initializing DockSession...");
            _session = new DockSession(StartupOptions.Parse(e.Args));
            _session.Changed += (_, _) => Dispatcher.Invoke(RefreshTray);
            _session.TaskbarCreated += (_, _) => Dispatcher.Invoke(InitTray);
            AppLog.Write("Starting DockSession...");
            _session.Start();
            AppLog.Write("Initializing Tray...");
            InitTray();
            AppLog.Write("Refreshing Tray...");
            RefreshTray();
            AppLog.Write("OnStartup completed successfully.");
        }
        catch (Exception ex)
        {
            AppLog.Write("Fatal startup exception.", ex);
            MessageBox.Show(ex.ToString(), $"{ProductInfo.Name} Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Write($"App.OnExit called with exit code {e.ApplicationExitCode}.");
        UserAlerts.Raised -= OnAlert;
        _tray?.Dispose();
        _session?.Dispose();
        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (Exception)
            {
            }

            _mutex.Dispose();
        }

        base.OnExit(e);
    }

    private long _lastTrayClickTime;

    private void InitTray()
    {
        _tray?.Dispose();
        _tray = new NativeTrayIcon { Text = ProductInfo.Name };
        _tray.LeftClick += (_, _) => OnTrayLeftClick();
        _tray.DoubleClick += (_, _) => OnTrayDoubleClick();
        _tray.RightClick += (_, _) => Dispatcher.Invoke(() =>
        {
            if (_trayMenu is not null)
            {
                _trayMenu.Placement = PlacementMode.MousePoint;
                _trayMenu.IsOpen = true;
            }
        });
        RefreshTray();
    }

    private void OnTrayLeftClick()
    {
        var now = Environment.TickCount64;
        if (now - _lastTrayClickTime < 250)
        {
            return;
        }

        _lastTrayClickTime = now;
        _session?.ToggleSettings();
    }

    private void OnTrayDoubleClick()
    {
        _lastTrayClickTime = Environment.TickCount64;
        _session?.ShowSettings();
    }

    private void RefreshTray()
    {
        if (_tray is null || _session is null || _session.CurrentDiff is null)
        {
            return;
        }

        var diff = _session.CurrentDiff;
        var profile = _session.Settings.Profiles.FirstOrDefault(item => item.Id == diff.ProfileId);
        var profileName = profile is null ? diff.ProfileName : DefaultProfiles.DisplayName(profile);
        var text = _session.IsSafeMode
            ? UiText.SafeModeTray
            : ProductFeatures.ProfilesEnabled
                ? $"{ProductInfo.Name} — {profileName}"
                : ProductInfo.Name;
        _tray.Text = text;

        var menu = new ContextMenu();
        var settingsItem = new MenuItem { Header = UiText.Settings };
        settingsItem.Click += (_, _) => _session.ShowSettings();
        menu.Items.Add(settingsItem);

        if (ProductFeatures.ProfilesEnabled)
        {
            var undoItem = new MenuItem { Header = UiText.UndoLastProfile };
            undoItem.Click += (_, _) => _session.UndoProfile();
            menu.Items.Add(undoItem);
            menu.Items.Add(new Separator());
            foreach (var item in _session.Settings.Profiles)
            {
                var id = item.Id;
                var entry = new MenuItem
                {
                    Header = DefaultProfiles.DisplayName(item),
                    IsChecked = item.Id == diff.ProfileId
                };
                entry.Click += (_, _) => _session.SwitchProfile(id);
                menu.Items.Add(entry);
            }
        }

        menu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = UiText.Exit };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        _trayMenu = menu;
    }

    public void ShowTrayBalloon(string title, string text, bool isWarning = false) =>
        Dispatcher.Invoke(() => _tray?.ShowBalloon(title, text, isWarning));

    private void OnAlert(string title, string message) =>
        Dispatcher.Invoke(() => _tray?.ShowBalloon(title, message, isWarning: false));
}
