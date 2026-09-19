using System.Threading;
using System.Windows;
using WinDock.Core;
using WinDock.Infrastructure;
using WinForms = System.Windows.Forms;

namespace WinDock.App;

public partial class App : Application
{
    private DockSession? _session;
    private WinForms.NotifyIcon? _tray;
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Local\WinDock.SingleInstance", out var created);
        if (!created)
        {
            MessageBox.Show("WinDock 已经在运行。", "WinDock", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write("Unhandled exception.", args.Exception);
            args.Handled = true;
            _tray?.ShowBalloonTip(4000, "WinDock", "已记录到日志。", WinForms.ToolTipIcon.Warning);
        };
        UserAlerts.Raised += OnAlert;
        _session = new DockSession(StartupOptions.Parse(e.Args));
        _session.Changed += (_, _) => Dispatcher.Invoke(RefreshTray);
        _session.TaskbarCreated += (_, _) => Dispatcher.Invoke(InitTray);
        _session.Start();
        InitTray();
        RefreshTray();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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

    private void InitTray()
    {
        _tray?.Dispose();
        _tray = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "WinDock"
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                _session?.ShowSettings();
            }
        };
        _tray.DoubleClick += (_, _) => _session?.ShowSettings();
        RefreshTray();
    }

    private void RefreshTray()
    {
        if (_tray is null || _session is null || _session.CurrentDiff is null)
        {
            return;
        }

        var diff = _session.CurrentDiff;
        var text = _session.IsSafeMode
            ? "WinDock — 安全模式"
            : ProductFeatures.ProfilesEnabled
                ? $"WinDock — {diff.ProfileName}"
                : "WinDock";
        _tray.Text = text.Length <= 63 ? text : text[..63];

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("设置", null, (_, _) => _session.ShowSettings());
        if (ProductFeatures.ProfilesEnabled)
        {
            menu.Items.Add("撤销上一次情景", null, (_, _) => _session.UndoProfile());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            foreach (var profile in _session.Settings.Profiles)
            {
                var id = profile.Id;
                var item = new WinForms.ToolStripMenuItem(profile.Name)
                {
                    Checked = profile.Id == diff.ProfileId
                };
                item.Click += (_, _) => _session.SwitchProfile(id);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip?.Dispose();
        _tray.ContextMenuStrip = menu;
    }

    private void OnAlert(string title, string message) =>
        Dispatcher.Invoke(() => _tray?.ShowBalloonTip(4000, title, message, WinForms.ToolTipIcon.Info));
}
