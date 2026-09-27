using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.App;

public partial class InstalledAppPicker : Window
{
    private readonly IShellLaunch _shell;
    private readonly ObservableCollection<AppRow> _visible = [];
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<AppRow> _iconQueue = new();
    private readonly HashSet<string> _queued = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<StartMenuApp> _apps = [];
    private string _query = string.Empty;
    private int _iconWorkers;
    private int _iconGen;
    private bool _closed;

    public InstalledAppPicker(IShellLaunch shell, bool isLight = true)
    {
        _shell = shell;
        InitializeComponent();
        Title = UiText.AddAppTitle;
        PickerTitle.Text = UiText.StartMenuApps;
        SearchPlaceholder.Text = UiText.SearchApps;
        CancelButton.Content = UiText.Cancel;
        AddButton.Content = UiText.Add;
        ApplyTheme(isLight);
        AppList.ItemsSource = _visible;
        Loaded += (_, _) => _ = LoadAppsAsync();
        Closed += (_, _) => _closed = true;
    }

    private void ApplyTheme(bool isLight)
    {
        SettingsChrome.ApplyWindow(this, isLight);
        SettingsChrome.ApplySearch(SearchHost, SearchPlaceholder, SearchBox, isLight);
    }

    public IReadOnlyList<string> SelectedPaths { get; private set; } = [];

    private async Task LoadAppsAsync()
    {
        try
        {
            var apps = await Task.Run(() => _shell.ListStartMenuApps()).ConfigureAwait(true);
            if (_closed)
            {
                return;
            }

            _apps = apps;
            Rebuild();
        }
        catch (Exception)
        {
            _apps = [];
            Rebuild();
        }
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(_query) ? Visibility.Visible : Visibility.Collapsed;
        Rebuild();
    }

    private void Rebuild()
    {
        _visible.Clear();
        _iconQueue.Clear();
        _queued.Clear();
        _iconGen++;
        foreach (var app in _apps)
        {
            if (_query.Length > 0 && !app.Name.Contains(_query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            var row = new AppRow(app.Name, app.LnkPath);
            if (_icons.TryGetValue(app.LnkPath, out var icon))
            {
                row.Icon = icon;
            }

            _visible.Add(row);
        }
    }

    private void RowContainer_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBoxItem item)
        {
            return;
        }

        item.DataContextChanged -= RowContainer_OnDataContextChanged;
        item.DataContextChanged += RowContainer_OnDataContextChanged;
        RequestIcon(item.DataContext as AppRow);
    }

    private void RowContainer_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        RequestIcon(e.NewValue as AppRow);

    private void RequestIcon(AppRow? row)
    {
        if (row is null || _closed)
        {
            return;
        }

        if (_icons.TryGetValue(row.LnkPath, out var cached))
        {
            row.Icon = cached;
            return;
        }

        if (!_queued.Add(row.LnkPath))
        {
            return;
        }

        _iconQueue.Enqueue(row);
        PumpIcons(_iconGen);
    }

    private void PumpIcons(int gen)
    {
        while (!_closed && gen == _iconGen && _iconWorkers < 2 && _iconQueue.Count > 0)
        {
            var row = _iconQueue.Dequeue();
            _iconWorkers++;
            _ = Task.Run(() =>
            {
                string? path = null;
                try
                {
                    var pin = _shell.CreatePin(row.LnkPath);
                    path = _shell.GetIconFile(pin);
                }
                catch (Exception)
                {
                }

                _ = Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (!_closed && gen == _iconGen)
                        {
                            var icon = DecodeIcon(path);
                            _icons[row.LnkPath] = icon;
                            row.Icon = icon;
                        }
                    }
                    finally
                    {
                        _iconWorkers--;
                        PumpIcons(gen);
                    }
                }, DispatcherPriority.Background);
            });
        }
    }

    private static ImageSource? DecodeIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 32;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void AppList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void Add_OnClick(object sender, RoutedEventArgs e) => Accept();

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Accept()
    {
        if (AppList.SelectedItem is not AppRow row || string.IsNullOrWhiteSpace(row.LnkPath))
        {
            return;
        }

        SelectedPaths = [row.LnkPath];
        DialogResult = true;
    }

    private sealed class AppRow : INotifyPropertyChanged
    {
        private ImageSource? _icon;

        public AppRow(string name, string lnkPath)
        {
            Name = name;
            LnkPath = lnkPath;
        }

        public string Name { get; }

        public string LnkPath { get; }

        public ImageSource? Icon
        {
            get => _icon;
            set
            {
                if (ReferenceEquals(_icon, value))
                {
                    return;
                }

                _icon = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GlyphVisibility));
            }
        }

        public Visibility GlyphVisibility => _icon is null ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
