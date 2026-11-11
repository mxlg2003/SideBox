using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class DiskWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, IDiskConfigurable, IWidgetThemeAware, ILocalizable, IDockEdgeAware
{
    private readonly IDiskProvider _disk;
    private readonly EventHandler<DiskSnapshot> _updated;
    private readonly StackPanel _dockBody = new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly PhosphorGlyph _glyph = WidgetChrome.Glyph(PhosphorIcon.HardDrives, DockTokens.Disk);
    private readonly TextBlock _value = WidgetCopy.Primary();
    private readonly StackPanel _volumes = new();
    private readonly ComboBox _mainBox = new();
    private readonly StackPanel _volumeChecks = new();
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly Popup _popup;
    private TextBlock _flyoutTitle = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _mainCaption = null!;
    private DiskWidgetSettings _settings = DiskWidgetSettings.Default;
    private IReadOnlyList<DiskVolumeSnapshot> _all = [];
    private bool _applying;
    private bool _isLight = true;
    private bool _side;

    public DiskWidget(IDiskProvider disk)
    {
        _disk = disk;
        _updated = UiPump.Coalesce<DiskSnapshot>(Dispatcher, Render);
        _value.FontSize = 17;
        _glyph.Foreground = TokenBrushes.From(DockTokens.Disk);

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
        _popup.Closed += (_, _) => ShowEditor();
        _popup.Opened += (_, _) =>
        {
            var scale = (ScaleTransform)_panel.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _panel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };

        Loaded += (_, _) =>
        {
            _disk.Updated += _updated;
            Render(_disk.Current);
        };
        Unloaded += (_, _) =>
        {
            _disk.Updated -= _updated;
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
        Render(disk.Current);
    }

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public event EventHandler<DiskWidgetSettings>? SettingsChanged;

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
        _flyoutTitle.Text = BuiltInWidgets.DisplayName(BuiltInWidgets.Disk);
        _customizeTitle.Text = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _mainCaption.Text = UiText.Display;
        FillMainBox();
        _applying = false;
        Render(_disk.Current);
    }

    public void ApplySettings(DiskWidgetSettings settings)
    {
        _applying = true;
        _settings = DiskWidgetSettings.Normalize(settings);
        _disk.SetOptions(DiskSampleOptions.Default);
        PaintCustomize();
        RebuildTile();
        Render(_disk.Current);
        _applying = false;
    }

    public void Open()
    {
        ShowEditor();
        Render(_disk.Current);
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
        _value.Foreground = ink;
        _flyoutTitle.Foreground = ink;
        _customizeTitle.Foreground = ink;
        _backLabel.Foreground = ink;
        _mainCaption.Foreground = ink;
        foreach (var child in _volumeChecks.Children)
        {
            if (child is CheckBox box)
            {
                box.Foreground = ink;
            }
        }

        WidgetChrome.PaintSurfaces(_tile, _panel, _isLight);
        WidgetChrome.PaintCombo(_mainBox, _isLight);
        _glyph.Foreground = TokenBrushes.From(DockTokens.Disk);
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
        _glyph.Margin = _side ? new Thickness(0, 0, 0, 2) : new Thickness(0, 0, 6, 0);
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _value.VerticalAlignment = VerticalAlignment.Center;
        _value.HorizontalAlignment = _side ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _value.TextAlignment = _side ? TextAlignment.Center : TextAlignment.Left;
        _value.FontSize = _side ? DockTokens.PrimaryFontSize : 17;
        _value.TextWrapping = TextWrapping.NoWrap;
        _dockBody.Children.Add(_glyph);
        _dockBody.Children.Add(_value);
        PaintChrome();
    }

    private void Render(DiskSnapshot snapshot)
    {
        _all = snapshot.Volumes;
        var visible = DiskWidgetSettings.Visible(_all, _settings);
        _value.Text = DiskFormat.DockValue(visible, _settings);
        _value.Foreground = Ink();
        PaintVolumes(visible);
        if (_customizePage.Visibility == Visibility.Visible)
        {
            PaintVolumeChecks();
        }
    }

    private void PaintVolumes(IReadOnlyList<DiskVolumeSnapshot> volumes)
    {
        _volumes.Children.Clear();
        if (volumes.Count == 0)
        {
            var empty = WidgetCopy.Secondary();
            empty.Text = "—";
            empty.Foreground = Ink();
            empty.Margin = new Thickness(0, 12, 0, 0);
            _volumes.Children.Add(empty);
            return;
        }

        foreach (var volume in volumes)
        {
            var name = WidgetCopy.Primary();
            name.Text = DiskFormat.VolumeTitle(volume);
            name.TextWrapping = TextWrapping.Wrap;
            name.Foreground = Ink();
            var size = WidgetCopy.Secondary();
            size.Text = DiskFormat.Capacity(volume);
            size.Foreground = Ink();
            var bar = new SysActivityBar
            {
                Height = 6,
                Margin = new Thickness(0, 6, 0, 0),
                Progress = volume.UsedPercent / 100.0,
                Fill = TokenBrushes.From(DockTokens.Disk),
                Track = TokenBrushes.From(DockTokens.QuietFill)
            };
            var row = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(name);
            Grid.SetColumn(size, 1);
            head.Children.Add(size);
            row.Children.Add(head);
            row.Children.Add(bar);
            _volumes.Children.Add(row);
        }
    }

    private Grid BuildEditor()
    {
        _flyoutTitle = HeaderLabel(BuiltInWidgets.DisplayName(BuiltInWidgets.Disk));
        var customize = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize);
        var close = WidgetChrome.HeaderClose(ClosePopup, 18);
        var header = HeaderRow(_flyoutTitle, customize, close);
        var scroll = new ScrollViewer
        {
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _volumes
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

        _mainCaption = HeaderLabel(UiText.Display);
        _mainCaption.Opacity = 0.62;
        _mainCaption.Margin = new Thickness(0, 8, 0, 8);
        WidgetChrome.PaintCombo(_mainBox, true);
        FillMainBox();
        _mainBox.SelectionChanged += (_, _) =>
        {
            if (_applying || _mainBox.SelectedItem is not ComboChoice choice)
            {
                return;
            }

            _settings = _settings with { Main = DiskMains.Normalize(choice.Id) };
            Persist();
            RebuildTile();
            Render(_disk.Current);
        };

        var dockCaption = HeaderLabel(UiText.ShowOnDock);
        dockCaption.Opacity = 0.62;
        dockCaption.Margin = new Thickness(0, 8, 0, 8);
        var checks = new ScrollViewer
        {
            MaxHeight = 180,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _volumeChecks
        };
        WidgetChrome.PaintScrollViewer(checks, true);
        var page = new StackPanel
        {
            Children = { header, _mainCaption, _mainBox, dockCaption, checks }
        };
        var grid = new Grid();
        grid.Children.Add(page);
        return grid;
    }

    private void FillMainBox()
    {
        var selected = _mainBox.SelectedItem is ComboChoice choice ? choice.Id : _settings.Main;
        _mainBox.Items.Clear();
        foreach (var main in DiskMains.All)
        {
            _mainBox.Items.Add(new ComboChoice(main, DiskFormat.MainTitle(main)));
        }

        WidgetChrome.SelectChoice(_mainBox, selected);
    }

    private void PaintCustomize()
    {
        WidgetChrome.SelectChoice(_mainBox, _settings.Main);
        PaintVolumeChecks(true);
    }

    private string _volumeKey = "";

    private void PaintVolumeChecks(bool force = false)
    {
        var key = string.Join("|", _all.Select(volume => volume.Id));
        if (!force && key == _volumeKey && _volumeChecks.Children.Count == _all.Count)
        {
            return;
        }

        _volumeKey = key;
        _volumeChecks.Children.Clear();
        foreach (var volume in _all)
        {
            var box = new CheckBox
            {
                Content = DiskFormat.VolumeTitle(volume),
                IsChecked = _settings.IsVisible(volume.Id),
                Foreground = Ink(),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var id = volume.Id;
            box.Checked += (_, _) => SetVolume(id, true);
            box.Unchecked += (_, _) => SetVolume(id, false);
            _volumeChecks.Children.Add(box);
        }
    }

    private void SetVolume(string id, bool visible)
    {
        if (_applying)
        {
            return;
        }

        var hidden = _settings.HiddenVolumeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (visible)
        {
            hidden.Remove(id);
        }
        else
        {
            hidden.Add(id);
        }

        _settings = _settings with { HiddenVolumeIds = hidden.ToArray() };
        Persist();
        Render(_disk.Current);
    }

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
