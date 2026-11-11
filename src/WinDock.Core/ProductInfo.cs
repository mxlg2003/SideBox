namespace WinDock.Core;

public static class ProductInfo
{
    public const string Name = "SideBox";
    public const string MutexName = @"Local\SideBox.SingleInstance";
    public const string StartupValueName = "SideBox";
    public const string LegacyStartupValueName = "WinDock";
    public const string LegacyStartupValueName2 = "Sidebox";
    public const string DataFolderName = "SideBox";
    public const string LegacyDataFolderName = "WinDock";
    public const string LegacyDataFolderName2 = "Sidebox";

    public const string GitHubOwner = "mxlg2003";
    public const string GitHubRepo = "SideBox";
    public const string GitHubRepositoryUrl = "https://github.com/mxlg2003/SideBox";
    public const string GitHubReleasesUrl = "https://github.com/mxlg2003/SideBox/releases";
    public const string GitHubLatestReleaseUrl = "https://github.com/mxlg2003/SideBox/releases/latest";
    public const string GitHubLatestJsonUrl = "https://github.com/mxlg2003/SideBox/releases/latest/download/latest.json";
    public const string GitHubLatestApiUrl = "https://api.github.com/repos/mxlg2003/SideBox/releases/latest";
    public const string DownloadMirrorPrefix = "https://ghfast.top/";

    public static string LocalData(params string[] relative)
    {
        var parts = new string[relative.Length + 2];
        parts[0] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        parts[1] = DataFolderName;
        if (relative.Length > 0)
        {
            Array.Copy(relative, 0, parts, 2, relative.Length);
        }

        return Path.Combine(parts);
    }
}
