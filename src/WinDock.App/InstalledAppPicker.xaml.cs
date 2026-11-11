using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.App;

public partial class InstalledAppPicker : Window
{
    private readonly IShellLaunch _shell;
    private readonly ObservableCollection<object> _available = [];
    private readonly ObservableCollection<object> _chosen = [];
    private readonly List<AppRow> _picked = [];
    private readonly Dictionary<string, AppRow> _rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<AppRow> _iconQueue = new();
    private readonly HashSet<string> _queued = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<StartMenuApp> _apps = [];
    private string _query = string.Empty;
    private int _iconWorkers;
    private bool _closed;

    public InstalledAppPicker(IShellLaunch shell, bool isLight = true)
    {
        _shell = shell;
        InitializeComponent();
        Title = UiText.AddAppTitle;
        PickerTitle.Text = UiText.StartMenuApps;
        SearchPlaceholder.Text = UiText.SearchApps;
        ChosenEmpty.Text = UiText.ChosenEmpty;
        ToChosenButton.ToolTip = UiText.MoveToChosen;
        ToAvailableButton.ToolTip = UiText.MoveToAvailable;
        CancelButton.Content = UiText.Cancel;
        ShuttleButtons.Margin = new Thickness(DockTokens.Settings.CatalogCardGap, 0, DockTokens.Settings.CatalogCardGap, 0);
        ToAvailableButton.Margin = new Thickness(0, DockTokens.Settings.FieldGap, 0, 0);
        ApplyTheme(isLight);
        AppList.ItemsSource = _available;
        ChosenList.ItemsSource = _chosen;
        var indexWidth = DockTokens.GlyphSize + DockTokens.Settings.FieldGap;
        var indexMargin = new Thickness(DockTokens.Settings.FieldGap, 0, 0, 0);
        AvailableIndex.Width = indexWidth;
        ChosenIndex.Width = indexWidth;
        AvailableIndex.Margin = indexMargin;
        ChosenIndex.Margin = indexMargin;
        WireRail(AvailableIndex, AppList, _available);
        WireRail(ChosenIndex, ChosenList, _chosen);
        RefreshActions();
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
        var picked = new HashSet<string>(_picked.Select(row => row.LnkPath), StringComparer.OrdinalIgnoreCase);
        var available = new List<AppRow>();
        foreach (var app in _apps)
        {
            if (picked.Contains(app.LnkPath))
            {
                continue;
            }

            if (_query.Length > 0 && !app.Name.Contains(_query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            available.Add(RowFor(app));
        }

        Fill(_available, available);
        Fill(_chosen, _picked);
        PaintRail(AvailableIndex, _available);
        PaintRail(ChosenIndex, _chosen);
        RefreshActions();
    }

    private static void Fill(ObservableCollection<object> target, IEnumerable<AppRow> rows)
    {
        target.Clear();
        string? letter = null;
        foreach (var row in rows.OrderBy(row => row.Name, Comparer<string>.Create(NameIndex.Compare)))
        {
            var next = NameIndex.Letter(row.Name);
            if (!string.Equals(letter, next, StringComparison.Ordinal))
            {
                letter = next;
                target.Add(new LetterRow(letter));
            }

            target.Add(row);
        }
    }

    private AppRow RowFor(StartMenuApp app)
    {
        if (_rows.TryGetValue(app.LnkPath, out var existing))
        {
            return existing;
        }

        var row = new AppRow(app.Name, app.LnkPath);
        if (_icons.TryGetValue(app.LnkPath, out var icon))
        {
            row.Icon = icon;
        }

        _rows[app.LnkPath] = row;
        return row;
    }

    private void Lists_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshActions();

    private void ToChosen_OnClick(object sender, RoutedEventArgs e) =>
        MoveToChosen(OrderedSelection(AppList, _available));

    private void ToAvailable_OnClick(object sender, RoutedEventArgs e) =>
        MoveToAvailable(OrderedSelection(ChosenList, _chosen));

    private void Available_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RowFromClick(e) is AppRow row)
        {
            MoveToChosen([row]);
        }
    }

    private void Chosen_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RowFromClick(e) is AppRow row)
        {
            MoveToAvailable([row]);
        }
    }

    private void MoveToChosen(IReadOnlyList<AppRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var chosen = new HashSet<string>(_picked.Select(row => row.LnkPath), StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (chosen.Add(row.LnkPath))
            {
                _picked.Add(row);
            }
        }

        Rebuild();
    }

    private void MoveToAvailable(IReadOnlyList<AppRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            _picked.Remove(row);
        }

        Rebuild();
    }

    private void RefreshActions()
    {
        ToChosenButton.IsEnabled = AppList.SelectedItems.Count > 0;
        ToAvailableButton.IsEnabled = ChosenList.SelectedItems.Count > 0;
        AddButton.IsEnabled = _picked.Count > 0;
        AddButton.Content = UiText.AddSelected(_picked.Count);
        ChosenTitle.Text = UiText.ChosenApps(_picked.Count);
        ChosenEmpty.Visibility = _picked.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static List<AppRow> OrderedSelection(ListBox list, System.Collections.IList source) =>
        list.SelectedItems.OfType<AppRow>().OrderBy(source.IndexOf).ToList();

    private static AppRow? RowFromClick(MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not ListBoxItem)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        return (source as ListBoxItem)?.DataContext as AppRow;
    }

    private void RowContainer_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBoxItem item)
        {
            return;
        }

        item.DataContextChanged -= RowContainer_OnDataContextChanged;
        item.DataContextChanged += RowContainer_OnDataContextChanged;
        PrepareContainer(item);
    }

    private void RowContainer_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            PrepareContainer(item);
        }
    }

    private void PrepareContainer(ListBoxItem item)
    {
        if (item.DataContext is AppRow row)
        {
            item.IsHitTestVisible = true;
            item.Focusable = true;
            RequestIcon(row);
            return;
        }

        item.IsHitTestVisible = false;
        item.Focusable = false;
        item.IsSelected = false;
    }

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
        PumpIcons();
    }

    private void PumpIcons()
    {
        while (!_closed && _iconWorkers < 2 && _iconQueue.Count > 0)
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
                        if (!_closed)
                        {
                            var icon = DecodeIcon(path);
                            _icons[row.LnkPath] = icon;
                            row.Icon = icon;
                        }
                    }
                    finally
                    {
                        _iconWorkers--;
                        PumpIcons();
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

    private void WireRail(UniformGrid rail, ListBox list, ObservableCollection<object> items)
    {
        rail.Background = Brushes.Transparent;
        rail.PreviewMouseLeftButtonDown += (_, e) =>
        {
            JumpNearest(rail, list, items, e.GetPosition(rail));
            rail.CaptureMouse();
            e.Handled = true;
        };
        rail.PreviewMouseMove += (_, e) =>
        {
            if (rail.IsMouseCaptured)
            {
                JumpNearest(rail, list, items, e.GetPosition(rail));
            }
        };
        rail.PreviewMouseLeftButtonUp += (_, _) => rail.ReleaseMouseCapture();
    }

    private void PaintRail(UniformGrid rail, ObservableCollection<object> items)
    {
        var present = items.OfType<LetterRow>().Select(row => row.Letter).ToHashSet(StringComparer.Ordinal);
        var enabledBrush = (Brush)FindResource("Settings.Text");
        rail.Background = Brushes.Transparent;
        rail.Children.Clear();
        rail.Visibility = present.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var symbol in NameIndex.Rail)
        {
            var letter = symbol.ToString();
            if (!present.Contains(letter))
            {
                continue;
            }

            rail.Children.Add(new Border
            {
                Background = Brushes.Transparent,
                Tag = letter,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = letter,
                    FontSize = DockTokens.SecondaryFontSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = enabledBrush,
                    IsHitTestVisible = false
                }
            });
        }
    }

    private static void JumpNearest(UniformGrid rail, ListBox list, ObservableCollection<object> items, Point point)
    {
        string? letter = null;
        var best = double.MaxValue;
        foreach (var child in rail.Children.OfType<Border>())
        {
            if (child.Tag is not string childLetter)
            {
                continue;
            }

            var top = child.TranslatePoint(new Point(0, 0), rail).Y;
            var height = Math.Max(child.ActualHeight, 1);
            if (point.Y >= top && point.Y < top + height)
            {
                letter = childLetter;
                break;
            }

            var distance = Math.Abs(point.Y - (top + (height / 2)));
            if (distance < best)
            {
                best = distance;
                letter = childLetter;
            }
        }

        if (letter is null)
        {
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not LetterRow row || !string.Equals(row.Letter, letter, StringComparison.Ordinal))
            {
                continue;
            }

            list.ScrollIntoView(items[index]);
            list.UpdateLayout();
            if (FindScrollViewer(list) is ScrollViewer viewer)
            {
                viewer.ScrollToVerticalOffset(index);
            }

            return;
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            var nested = FindScrollViewer(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void Add_OnClick(object sender, RoutedEventArgs e) => Accept();

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Accept()
    {
        if (_picked.Count == 0)
        {
            return;
        }

        SelectedPaths = _picked
            .OrderBy(row => row.Name, Comparer<string>.Create(NameIndex.Compare))
            .Select(row => row.LnkPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (SelectedPaths.Count == 0)
        {
            return;
        }

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

public sealed class LetterRow
{
    public LetterRow(string letter) => Letter = letter;

    public string Letter { get; }
}

public sealed class PickerTemplateSelector : DataTemplateSelector
{
    public DataTemplate? AppTemplate { get; set; }

    public DataTemplate? LetterTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is LetterRow ? LetterTemplate : AppTemplate;
}
