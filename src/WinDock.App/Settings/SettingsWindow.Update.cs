using System.Diagnostics;
using System.Windows;
using WinDock.Core;

namespace WinDock.App;

public partial class SettingsWindow
{
    private AppVersionInfo? _availableUpdate;
    private bool _updateBusy;
    private CancellationTokenSource? _updateCts;

    private async void AboutCheckUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        if (_updateBusy)
        {
            return;
        }

        _updateBusy = true;
        AboutCheckUpdateButton.IsEnabled = false;
        UpdateStatusHost.Visibility = Visibility.Visible;
        UpdateStatusTitle.Text = UiText.CheckingForUpdates;
        UpdateStatusMessage.Text = "";
        UpdateNowButton.Visibility = Visibility.Collapsed;
        ViewReleaseNotesButton.Visibility = Visibility.Collapsed;
        OpenBrowserDownloadButton.Visibility = Visibility.Collapsed;
        UpdateChangelogTitle.Visibility = Visibility.Collapsed;
        UpdateChangelogHost.Visibility = Visibility.Collapsed;
        UpdateProgressBar.Visibility = Visibility.Collapsed;

        _updateCts?.Cancel();
        _updateCts = new CancellationTokenSource();

        try
        {
            var currentVersion = typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);

            var checkResult = await UpdateService.CheckUpdateAsync(currentVersion, _updateCts.Token);
            if (!checkResult.Success)
            {
                UpdateStatusTitle.Text = UiText.UpdateCheckFailed;
                UpdateStatusMessage.Text = checkResult.Error ?? "";
                ViewReleaseNotesButton.Visibility = Visibility.Visible;
                return;
            }

            if (!checkResult.HasUpdate || checkResult.Info is null)
            {
                UpdateStatusTitle.Text = UiText.UpToDate;
                UpdateStatusMessage.Text = "";
                _availableUpdate = null;
                return;
            }

            _availableUpdate = checkResult.Info;
            UpdateStatusTitle.Text = UiText.UpdateAvailable(checkResult.Info.Version);
            UpdateNowButton.Visibility = Visibility.Visible;
            UpdateNowButton.IsEnabled = true;

            if (!string.IsNullOrWhiteSpace(checkResult.Info.ReleasePageUrl))
            {
                ViewReleaseNotesButton.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrWhiteSpace(checkResult.Info.Changelog))
            {
                UpdateChangelogTitle.Visibility = Visibility.Visible;
                UpdateChangelogHost.Visibility = Visibility.Visible;
                UpdateChangelogText.Text = checkResult.Info.Changelog;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusTitle.Text = UiText.UpdateCheckFailed;
            UpdateStatusMessage.Text = ex.Message;
            ViewReleaseNotesButton.Visibility = Visibility.Visible;
        }
        finally
        {
            _updateBusy = false;
            AboutCheckUpdateButton.IsEnabled = true;
        }
    }

    private async void UpdateNow_OnClick(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null || _updateBusy)
        {
            return;
        }

        _updateBusy = true;
        AboutCheckUpdateButton.IsEnabled = false;
        UpdateNowButton.IsEnabled = false;
        OpenBrowserDownloadButton.Visibility = Visibility.Collapsed;
        UpdateProgressBar.Visibility = Visibility.Visible;
        UpdateProgressBar.Value = 0;
        UpdateStatusMessage.Text = UiText.DownloadingUpdate(0);

        _updateCts?.Cancel();
        _updateCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<int>(pct =>
            {
                UpdateProgressBar.Value = pct;
                UpdateStatusMessage.Text = UiText.DownloadingUpdate(pct);
            });

            await UpdateService.DownloadAndInstallAsync(_availableUpdate, progress, _updateCts.Token);
        }
        catch (Exception ex)
        {
            UpdateStatusTitle.Text = UiText.UpdateDownloadFailed;
            UpdateStatusMessage.Text = $"{ex.Message} {UiText.UpdateDownloadFailedWithFallback}";
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            UpdateNowButton.IsEnabled = true;
            AboutCheckUpdateButton.IsEnabled = true;
            OpenBrowserDownloadButton.Visibility = Visibility.Visible;
            _updateBusy = false;
        }
    }

    private void ViewReleaseNotes_OnClick(object sender, RoutedEventArgs e)
    {
        var url = _availableUpdate?.ReleasePageUrl ?? ProductInfo.GitHubLatestReleaseUrl;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void OpenBrowserDownload_OnClick(object sender, RoutedEventArgs e)
    {
        var url = _availableUpdate?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = _availableUpdate?.ReleasePageUrl ?? ProductInfo.GitHubLatestReleaseUrl;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
