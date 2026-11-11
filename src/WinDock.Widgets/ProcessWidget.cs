using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class ProcessWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IProcessConfigurable, IPrivacyAware, IWidgetThemeAware, ILocalizable, IDockEdgeAware
{
    private readonly IProcessListProvider _processes;
    private readonly EventHandler<ProcessListSnapshot> _updated;
    private readonly StackPanel _dockBody = new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Image _dockIcon = new()
    {
        Width = 20,
        Height = 20,
        Stretch = Stretch.Uniform,
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    private readonly PhosphorGlyph _glyph = WidgetChrome.Glyph(PhosphorIcon.AppWindow, DockTokens.Cpu);
    private readonly TextBlock _name = WidgetCopy.Primary();
    private readonly TextBlock _metric = WidgetCopy.Secondary();
    private readonly StackPanel _list = new();
    private readonly ComboBox _sortBox = new();
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _sortCaption = null!;
    private readonly List<ProcessLine> _lines = [];
    private ProcessWidgetSettings _settings = ProcessWidgetSettings.Default;
    private bool _applying;
    private bool _isLight = true;
    private bool _side;

    public ProcessWidget(IProcessListProvider processes)
    {
        _processes = processes;
        _updated = UiPump.Coalesce<ProcessListSnapshot>(Dispatcher, Render);
        _name.FontSize = 13;
        _name.MaxWidth = 96;
        _name.TextWrapping = TextWrapping.NoWrap;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _metric.FontSize = 11;
        _tile = WidgetChrome.Wrap(_dockBody);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        _editorPage = BuildEditor();
        _customizePage = BuildCustomize();
        _customizePage.Visibility = Visibility.Collapsed;
        var body = new Grid();
        body.Children.Add(_editorPage);
        body.Children.Add(_customizePage);
        _panel = WidgetChrome.Flyout(body, 360);
        _panel.RenderTransformOrigin = new Point(0.5, 0);
        _panel.RenderTransform = new ScaleTransform(1, 1);

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8,
            Child = _panel
        };
        WidgetFlyout.Attach(_popup);
        _popup.Closed += (_, _) =>
        {
            _processes.SetLive(false);
            ShowEditor();
        };
        _popup.Opened += (_, _) =>
        {
            _processes.SetLive(true);
            var scale = (ScaleTransform)_panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _panel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };

        Loaded += (_, _) =>
        {
            _processes.Updated += _updated;
            Render(_processes.Current);
        };
        Unloaded += (_, _) =>
        {
            _processes.Updated -= _updated;
            _processes.SetLive(false);
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ClipToBounds = false;
        MaxHeight = DockTokens.TileHeight;
        ApplyTheme(true);
        RebuildTile();
        Render(processes.Current);
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<ProcessWidgetSettings>? SettingsChanged;

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        RebuildTile();
        WidgetChrome.FitEdge(_tile, _side);
        WidgetChrome.FitHost(this, _side);
    }

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        Foreground = Ink();
        PaintChrome();
    }

    public void ApplyLocale()
    {
        _applying = true;
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Processes);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _sortCaption.Text = UiText.ProcessSort;
        FillSortBox();
        _applying = false;
        Render(_processes.Current);
    }

    public void ApplySettings(ProcessWidgetSettings settings)
    {
        _applying = true;
        _settings = ProcessWidgetSettings.Normalize(settings);
        PaintCustomize();
        RebuildTile();
        Render(_processes.Current);
        _applying = false;
    }

    public void OnPrivacyChanged() => Render(_processes.Current);

    public void Open()
    {
        ShowEditor();
        Render(_processes.Current);
        PaintChrome();
        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
        _popup.IsOpen = true;
    }

    public void OpenCustomize()
    {
        ShowCustomize();
        PaintChrome();
        WidgetChrome.ApplyTree(_panel, _isLight, WidgetChrome.ThemeBackdrop);
        _popup.IsOpen = true;
    }

    private Brush Ink() => TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);

    private void PaintChrome()
    {
        var ink = Ink();
        Foreground = ink;
        _name.Foreground = ink;
        _metric.Foreground = ink;
        _flyoutTitle.Foreground = ink;
        _customizeTitle.Foreground = ink;
        _backLabel.Foreground = ink;
        _sortCaption.Foreground = ink;
        WidgetChrome.PaintSurfaces(_tile, _panel, _isLight);
        WidgetChrome.PaintCombo(_sortBox, _isLight);
        _glyph.Foreground = TokenBrushes.From(DockTokens.Cpu);
    }

    private void Persist()
    {
        if (_applying)
        {
            return;
        }

        SettingsChanged?.Invoke(this, _settings);
    }

    private void RebuildTile()
    {
        _dockBody.Children.Clear();
        _dockBody.Orientation = _side ? Orientation.Vertical : Orientation.Horizontal;
        _dockBody.HorizontalAlignment = HorizontalAlignment.Center;
        _dockIcon.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 8, 0);
        _dockIcon.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 8, 0);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _name.VerticalAlignment = VerticalAlignment.Center;
        _name.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _name.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _name.TextWrapping = TextWrapping.NoWrap;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _name.MaxWidth = _side ? DockTokens.SideTileWidth - 16 : double.PositiveInfinity;
        _metric.VerticalAlignment = VerticalAlignment.Center;
        _metric.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _metric.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _metric.Margin = _side ? new Thickness(0, 1, 0, 0) : new Thickness(6, 0, 0, 0);
        _metric.FontSize = _side ? DockTokens.PrimaryFontSize : DockTokens.SecondaryFontSize;
        _metric.FontWeight = _side ? FontWeights.SemiBold : FontWeights.Normal;
        _dockBody.Children.Add(_dockIcon);
        _dockBody.Children.Add(_glyph);
        if (_side)
        {
            _dockBody.Children.Add(_metric);
            _dockBody.Children.Add(_name);
        }
        else
        {
            _dockBody.Children.Add(_name);
            _dockBody.Children.Add(_metric);
        }
        PaintChrome();
    }

    private void Render(ProcessListSnapshot snapshot)
    {
        if (!snapshot.Allowed)
        {
            _name.Text = UiText.ProcessNamesOff;
            _metric.Text = "";
            _name.Foreground = Ink();
            ShowDockIcon(null);
            ClearLines();
            var off = WidgetCopy.Secondary();
            off.Text = UiText.ProcessNamesOff;
            off.Foreground = Ink();
            off.TextWrapping = TextWrapping.Wrap;
            off.Margin = new Thickness(0, 12, 0, 0);
            _list.Children.Add(off);
            return;
        }

        var ranked = ProcessFormat.Rank(snapshot.Rows, _settings.Sort);
        if (ranked.Count == 0)
        {
            _name.Text = "—";
            _metric.Text = ProcessFormat.SortTitle(_settings.Sort);
            _name.Foreground = Ink();
            _metric.Foreground = Ink();
            ShowDockIcon(null);
            ClearLines();
            return;
        }

        var top = ranked[0];
        _name.Text = ProcessFormat.Metric(top, _settings.Sort);
        _metric.Text = "";
        _name.Foreground = Ink();
        _metric.Foreground = Ink();
        ShowDockIcon(null);
        PaintList(ranked);
    }

    private void ClearLines()
    {
        _lines.Clear();
        _list.Children.Clear();
    }

    private void PaintList(IReadOnlyList<ProcessRow> ranked)
    {
        if (_lines.Count == ranked.Count)
        {
            var same = true;
            for (var i = 0; i < ranked.Count; i++)
            {
                if (_lines[i].Pid != ranked[i].Pid)
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                for (var i = 0; i < ranked.Count; i++)
                {
                    _lines[i].Paint(ranked[i], Ink());
                }

                return;
            }
        }

        ClearLines();
        foreach (var row in ranked)
        {
            var line = ProcessLine.Create(row, Ink());
            _lines.Add(line);
            _list.Children.Add(line.Root);
        }
    }

    private void ShowDockIcon(string? iconPath)
    {
        var image = ImageThumbCache.Load(iconPath, 20);
        if (image is null)
        {
            _dockIcon.Source = null;
            _dockIcon.Visibility = Visibility.Collapsed;
            _glyph.Visibility = Visibility.Visible;
            return;
        }

        _dockIcon.Source = image;
        _dockIcon.Visibility = Visibility.Visible;
        _glyph.Visibility = Visibility.Collapsed;
    }

    private sealed class ProcessLine
    {
        public int Pid { get; private set; }

        public Grid Root { get; private set; } = null!;

        private TextBlock _name = null!;
        private TextBlock _cpu = null!;
        private TextBlock _mem = null!;
        private Image _icon = null!;
        private string? _iconPath;

        public static ProcessLine Create(ProcessRow row, Brush ink)
        {
            var line = new ProcessLine();
            line._name = WidgetCopy.Primary();
            line._name.TextWrapping = TextWrapping.Wrap;
            line._cpu = WidgetCopy.Secondary();
            line._mem = WidgetCopy.Secondary();
            line._icon = new Image
            {
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var metrics = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { line._cpu, line._mem }
            };
            var copy = new Grid();
            copy.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            copy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            copy.Children.Add(line._icon);
            Grid.SetColumn(line._name, 1);
            copy.Children.Add(line._name);
            var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(copy);
            Grid.SetColumn(metrics, 1);
            grid.Children.Add(metrics);
            line.Root = grid;
            line.Paint(row, ink);
            return line;
        }

        public void Paint(ProcessRow row, Brush ink)
        {
            Pid = row.Pid;
            _name.Text = row.Name;
            _name.Foreground = ink;
            _cpu.Text = ProcessFormat.Cpu(row.CpuPercent);
            _cpu.Foreground = ink;
            _mem.Text = ProcessFormat.Memory(row.WorkingSetBytes);
            _mem.Foreground = ink;
            if (string.Equals(_iconPath, row.IconPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _iconPath = row.IconPath;
            _icon.Source = ImageThumbCache.Load(row.IconPath, 20);
            _icon.Visibility = _icon.Source is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Processes));
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        var scroll = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _list
        };
        WidgetChrome.PaintScrollViewer(scroll, true);
        var page = new StackPanel { Children = { header, scroll } };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private Grid BuildCustomize()
    {
        _backLabel = HeaderLabel(UiText.Back);
        var back = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Children =
            {
                WidgetChrome.HeaderIcon(PhosphorIcon.CaretLeft, ShowEditor, 12),
                _backLabel
            }
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowEditor();
            e.Handled = true;
        };
        _customizeTitle = HeaderLabel(UiText.Customize);
        _customizeTitle.Margin = new Thickness(8, 0, 0, 0);
        _customizeTitle.FontWeight = FontWeights.SemiBold;
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { back, _customizeTitle }
        }, close);

        _sortCaption = HeaderLabel(UiText.ProcessSort);
        _sortCaption.Opacity = 0.62;
        _sortCaption.Margin = new Thickness(0, 8, 0, 8);
        WidgetChrome.PaintCombo(_sortBox, true);
        FillSortBox();
        _sortBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _sortBox.SelectedItem is not ComboChoice choice)
            {
                return;
            }

            _settings = _settings with { Sort = ProcessSorts.Normalize(choice.Id) };
            Persist();
            Render(_processes.Current);
        };

        var page = new StackPanel { Children = { header, _sortCaption, _sortBox } };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private void FillSortBox()
    {
        var selected = _sortBox.SelectedItem is ComboChoice choice ? choice.Id : _settings.Sort;
        _sortBox.Items.Clear();
        foreach (var sort in ProcessSorts.All)
        {
            _sortBox.Items.Add(new ComboChoice(sort, ProcessFormat.SortTitle(sort)));
        }

        WidgetChrome.SelectChoice(_sortBox, selected);
    }

    private void PaintCustomize() => WidgetChrome.SelectChoice(_sortBox, _settings.Sort);

    private void ShowEditor()
    {
        _popup.StaysOpen = false;
        _customizePage.Visibility = Visibility.Collapsed;
        _editorPage.Visibility = Visibility.Visible;
    }

    private void ShowCustomize()
    {
        _popup.StaysOpen = true;
        PaintCustomize();
        _editorPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Visible;
    }

    private void ClosePopup()
    {
        _popup.StaysOpen = false;
        _popup.IsOpen = false;
    }

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(left);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (var item in right)
        {
            actions.Children.Add(item);
        }

        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        return row;
    }

    private static TextBlock HeaderLabel(string text) => new()
    {
        Text = text,
        FontFamily = WidgetFonts.Ui,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center
    };

}
