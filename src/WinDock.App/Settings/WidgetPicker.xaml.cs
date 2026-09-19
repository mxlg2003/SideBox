using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;
using WinDock.Widgets;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace WinDock.App;

public partial class WidgetPicker : UserControl
{
    private static readonly FontFamily GlyphFont = new("Segoe MDL2 Assets");
    private static readonly FontFamily UiFont = new("Segoe UI Variable, Segoe UI");
    private SolidColorBrush _textBrush = Freeze(DockTokens.Settings.Light.Text);
    private SolidColorBrush _mutedBrush = Freeze(DockTokens.Settings.Light.Muted);
    private SolidColorBrush _previewBrush = Freeze(DockTokens.Settings.Light.Preview);
    private SolidColorBrush _navSelected = Freeze(DockTokens.Settings.Light.NavSelected);
    private readonly SolidColorBrush _accent = Freeze(DockTokens.Settings.Accent);

    private DockSession? _session;
    private string _categoryId = WidgetCatalog.AllId;
    private string _query = string.Empty;
    private string _profileName = "岛上";
    private bool _suppress;
    private bool _catalogActive;
    private readonly List<Action<DateTime>> _clockTicks = [];
    private DispatcherTimer? _clockTimer;

    public WidgetPicker()
    {
        InitializeComponent();
        CatalogNavColumn.Width = new GridLength(DockTokens.Settings.CatalogNavWidth);
        Loaded += (_, _) => SyncClockTimer();
        Unloaded += (_, _) => _clockTimer?.Stop();
        IsVisibleChanged += (_, _) => SyncClockTimer();
    }

    public void Attach(DockSession session) => _session = session;

    public void SetProfileName(string name)
    {
        _profileName = string.IsNullOrWhiteSpace(name) ? "岛上" : name;
    }

    public void SetCatalogActive(bool active)
    {
        _catalogActive = active;
        SyncClockTimer();
    }

    public void ApplyTheme(bool isLight, bool rebuild = true)
    {
        _textBrush = Freeze(DockTokens.Settings.Text(isLight));
        _mutedBrush = Freeze(DockTokens.Settings.Muted(isLight));
        _previewBrush = Freeze(DockTokens.Settings.Preview(isLight));
        _navSelected = Freeze(DockTokens.Settings.NavSelected(isLight));
        if (NavPane is not null)
        {
            NavPane.Background = Freeze(DockTokens.Settings.NavPane(isLight));
            NavPane.BorderBrush = Freeze(DockTokens.Settings.Stroke(isLight));
        }

        if (CatalogScroll is not null)
        {
            CatalogScroll.Background = Freeze(DockTokens.Settings.Catalog(isLight));
        }

        if (SearchHost is not null)
        {
            SettingsChrome.ApplySearch(SearchHost, SearchPlaceholder, SearchBox, isLight);
        }

        if (rebuild)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_session is null)
        {
            return;
        }

        _suppress = true;
        if (SearchBox.Text != _query)
        {
            SearchBox.Text = _query;
        }

        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        RebuildNav();
        RebuildCatalog();
        _suppress = false;
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress)
        {
            return;
        }

        _query = SearchBox.Text.Trim();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        RebuildCatalog();
    }

    private void RebuildNav()
    {
        NavHost.Children.Clear();
        foreach (var category in WidgetCatalog.Nav)
        {
            var selected = string.Equals(category.Id, _categoryId, StringComparison.Ordinal);
            var button = new Border
            {
                CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 4),
                Background = selected ? _navSelected : Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = category.Id,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = category.Glyph,
                            FontFamily = GlyphFont,
                            FontSize = 14,
                            Width = 20,
                            Opacity = 0.78,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0)
                        },
                        new TextBlock
                        {
                            Text = category.Title,
                            FontSize = 13,
                            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = _textBrush
                        }
                    }
                }
            };
            button.MouseLeftButtonUp += (_, _) =>
            {
                if (button.Tag is not string id || string.Equals(_categoryId, id, StringComparison.Ordinal))
                {
                    return;
                }

                _categoryId = id;
                RebuildNav();
                RebuildCatalog();
            };
            NavHost.Children.Add(button);
        }
    }

    private void RebuildCatalog()
    {
        CatalogHost.Children.Clear();
        _clockTicks.Clear();
        if (_session is null)
        {
            return;
        }

        var groups = WidgetCatalog.VisibleGroups(_categoryId, _query);
        if (groups.Count == 0)
        {
            CatalogHost.Children.Add(EmptyState("没有匹配的模块", "换个关键词，或选左侧分类。"));
            return;
        }

        foreach (var group in groups)
        {
            CatalogHost.Children.Add(new TextBlock
            {
                Text = group.Title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, DockTokens.Settings.FieldGap)
            });
            var grid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, DockTokens.Settings.SectionGap) };
            foreach (var id in group.WidgetIds)
            {
                grid.Children.Add(CreateCard(id));
            }

            CatalogHost.Children.Add(grid);
        }

        SyncClockTimer();
        PulseClocks();
    }

    private UIElement EmptyState(string title, string hint)
    {
        var icon = new TextBlock
        {
            Text = "\uE721",
            FontFamily = GlyphFont,
            FontSize = 24,
            Foreground = _mutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = _textBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var body = new TextBlock
        {
            Text = hint,
            FontSize = 13,
            Foreground = _mutedBrush,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, DockTokens.Settings.FieldGap, 0, 0)
        };
        return new StackPanel
        {
            Margin = new Thickness(DockTokens.Settings.PagePad, DockTokens.Settings.SectionGap * 2, DockTokens.Settings.PagePad, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { icon, heading, body }
        };
    }

    private UIElement CreateCard(string widgetId)
    {
        var count = _session?.CurrentDiff.Dock.Items.Count(item =>
                        item.Type == DockItemType.WidgetChip
                        && string.Equals(item.WidgetId, widgetId, StringComparison.Ordinal)) ?? 0;
        var addHint = ProductFeatures.ProfilesEnabled ? $"添加到「{_profileName}」" : "添加到岛上";
        var removeHint = ProductFeatures.ProfilesEnabled ? "从当前情景拿掉一份" : "从岛上拿掉一份";
        var add = IconButton("+", addHint, () => _session?.AddWidget(widgetId));
        var remove = IconButton("−", removeHint, () => _session?.RemoveLastWidget(widgetId));
        remove.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var preview = new Border
        {
            Height = 96,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Background = _previewBrush,
            Padding = new Thickness(16, 12, 16, 12),
            ToolTip = BuiltInWidgets.Blurb(widgetId),
            Child = CreatePreview(widgetId)
        };

        var title = BuiltInWidgets.DisplayName(widgetId);
        if (count > 0)
        {
            title += $"（已添加 {count}）";
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { remove, add }
        };
        var card = new StackPanel { Margin = new Thickness(0, 0, 16, 16) };
        card.Children.Add(preview);
        card.Children.Add(new DockPanel
        {
            Margin = new Thickness(4, 8, 4, 0),
            Children =
            {
                actions,
                new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 13,
                            Foreground = _textBrush
                        },
                        new TextBlock
                        {
                            Text = BuiltInWidgets.Blurb(widgetId),
                            FontSize = 11,
                            Foreground = _mutedBrush,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 0)
                        }
                    }
                }
            }
        });
        DockPanel.SetDock(actions, Dock.Right);
        return card;
    }

    private Button IconButton(string content, string tooltip, Action click)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = tooltip,
            Width = 28,
            Height = 28,
            FontSize = 18
        };
        if (TryFindResource("Settings.GhostButton") is Style style)
        {
            button.Style = style;
            button.Foreground = _mutedBrush;
        }
        else
        {
            button.FontWeight = FontWeights.Normal;
            button.Foreground = _mutedBrush;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Cursor = System.Windows.Input.Cursors.Hand;
            button.VerticalAlignment = VerticalAlignment.Center;
        }

        button.Click += (_, _) => click();
        return button;
    }

    private UIElement CreatePreview(string widgetId)
    {
        var hub = _session!.Hub;
        return widgetId switch
        {
            BuiltInWidgets.Clock => ClockPreview(hub.Clock.LocalNow),
            BuiltInWidgets.Notes => NotesPreview(hub.Notes.Load("preview")),
            BuiltInWidgets.Power => PowerPreview(hub.Power.Current),
            BuiltInWidgets.SysActivity => SysActivityPreview(hub.Perf.Current),
            BuiltInWidgets.Network => NetworkPreview(hub.Network.Current),
            BuiltInWidgets.NowPlaying => MediaPreview(hub.Media.Current),
            BuiltInWidgets.BingWallpaper => BingPreview(hub.Bing.Current),
            BuiltInWidgets.Stopwatch => StopwatchPreview(),
            BuiltInWidgets.Countdown => CountdownPreview(),
            BuiltInWidgets.FocusTimer => FocusTimerPreview(),
            BuiltInWidgets.TimeProgress => TimeProgressPreview(hub.Clock.LocalNow),
            _ => Label(BuiltInWidgets.DisplayName(widgetId), 14, FontWeights.SemiBold)
        };
    }

    private UIElement ClockPreview(DateTime now)
    {
        var time = Label(ClockFormat.Time(now), 20, FontWeights.SemiBold);
        var date = Label(ClockFormat.ExpandedDate(now), 11, FontWeights.Normal);
        date.Foreground = _mutedBrush;
        var face = new AnalogClockFace(40)
        {
            Time = now,
            Stroke = _textBrush,
            Margin = new Thickness(0, 0, 12, 0)
        };
        _clockTicks.Add(next =>
        {
            time.Text = ClockFormat.Time(next);
            date.Text = ClockFormat.ExpandedDate(next);
            face.Time = next;
        });
        return PreviewRow(face, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { time, date } });
    }

    private UIElement FocusTimerPreview()
    {
        var ring = new ProgressRing(40)
        {
            Progress = 0.86,
            Track = Freeze(DockTokens.QuietFill),
            Fill = Freeze(FocusTimerColors.Fill(FocusTimerColors.Default)),
            Margin = new Thickness(0, 0, 12, 0)
        };
        var time = Label(FocusTimerFormat.Time(TimeSpan.FromMinutes(25)), 20, FontWeights.SemiBold);
        var status = Label(FocusTimerFormat.Phase(FocusTimerPhase.Focus), 11, FontWeights.Normal);
        status.Foreground = _mutedBrush;
        return PreviewRow(ring, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { time, status } });
    }

    private UIElement TimeProgressPreview(DateTime now)
    {
        var percent = Label(TimeProgressFormat.PercentLabel(now, TimeProgressPeriods.Year), 18, FontWeights.SemiBold);
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        var caption = Label(TimeProgressFormat.Caption(now, TimeProgressPeriods.Year), 11, FontWeights.Normal);
        caption.Foreground = _mutedBrush;
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        var bars = new TimeProgressBars
        {
            Width = 120,
            Height = 12,
            BarCount = 28,
            Progress = TimeProgressFormat.Fraction(now, TimeProgressPeriods.Year),
            Fill = Freeze(_session?.IsLightTheme() == false ? DockTokens.Dark.Text : DockTokens.Light.Text),
            Track = Freeze(DockTokens.QuietFill),
            Margin = new Thickness(0, 6, 0, 0)
        };
        _clockTicks.Add(next =>
        {
            percent.Text = TimeProgressFormat.PercentLabel(next, TimeProgressPeriods.Year);
            caption.Text = TimeProgressFormat.Caption(next, TimeProgressPeriods.Year);
            bars.Progress = TimeProgressFormat.Fraction(next, TimeProgressPeriods.Year);
        });
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { percent, caption, bars }
        };
    }

    private UIElement StopwatchPreview()
    {
        var time = Label(StopwatchFormat.Time(TimeSpan.Zero), 22, FontWeights.SemiBold);
        var status = Label(StopwatchFormat.Ready, 12, FontWeights.Normal);
        status.Foreground = _mutedBrush;
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { time, status }
        };
    }

    private UIElement CountdownPreview()
    {
        var glyph = new TextBlock
        {
            Text = "\uE916",
            FontFamily = GlyphFont,
            FontSize = 22,
            Foreground = _textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        var time = Label(CountdownFormat.Time(TimeSpan.FromMinutes(5)), 20, FontWeights.SemiBold);
        var status = Label(CountdownFormat.Ready, 11, FontWeights.Normal);
        status.Foreground = _mutedBrush;
        return PreviewRow(glyph, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { time, status } });
    }

    private UIElement SysActivityPreview(PerfSnapshot snapshot)
    {
        return PreviewRow(
            PreviewMeter(SysActivityFormat.Percent(snapshot.CpuPercent, snapshot.CpuAvailable), "CPU", DockTokens.Cpu),
            PreviewMeter(SysActivityFormat.Percent(snapshot.MemoryPercent, snapshot.MemoryAvailable), "内存", DockTokens.Memory));
    }

    private UIElement PreviewMeter(string percent, string caption, uint tint)
    {
        var value = Label(percent, 18, FontWeights.SemiBold);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = Freeze(tint),
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var name = Label(caption, 11, FontWeights.Normal);
        name.Foreground = _mutedBrush;
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { dot, name }
        };
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            Children = { value, row }
        };
    }

    private UIElement NotesPreview(string text)
    {
        var preview = Label(NotesText.DockPreview(text), 13, FontWeights.SemiBold);
        preview.Foreground = Freeze(NotePapers.Ink);
        return new Border
        {
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Background = Freeze(NotePapers.Fill(NotePapers.Default)),
            Padding = new Thickness(12, 10, 12, 10),
            Child = preview
        };
    }

    private UIElement NetworkPreview(NetworkSnapshot snapshot)
    {
        var downArrow = Label("↓", 13, FontWeights.SemiBold);
        downArrow.Foreground = Freeze(DockTokens.Network);
        downArrow.Margin = new Thickness(0, 0, 6, 0);
        var upArrow = Label("↑", 13, FontWeights.SemiBold);
        upArrow.Foreground = Freeze(DockTokens.NetworkUp);
        upArrow.Margin = new Thickness(0, 0, 6, 0);
        var down = Label(snapshot.IsConnected ? ByteRate.Format(snapshot.ReceiveBytesPerSecond) : "未连接", 15, FontWeights.SemiBold);
        var up = Label(snapshot.IsConnected ? ByteRate.Format(snapshot.SendBytesPerSecond) : string.Empty, 15, FontWeights.SemiBold);
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Children = { downArrow, down } },
                new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0), Children = { upArrow, up } }
            }
        };
    }

    private UIElement PowerPreview(PowerSnapshot snapshot)
    {
        var devices = snapshot.Devices.Count > 0 ? snapshot.Devices : PreviewDevices(snapshot);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var device in devices.Take(4))
        {
            row.Children.Add(PowerPreviewCell(device));
        }

        return row;
    }

    private static IReadOnlyList<PowerDeviceSnapshot> PreviewDevices(PowerSnapshot snapshot) =>
    [
        new()
        {
            Id = "preview-laptop",
            Name = "电脑",
            Kind = PowerDeviceKind.Laptop,
            Percent = snapshot.PercentRemaining ?? 76
        },
        new()
        {
            Id = "preview-audio",
            Name = "耳机",
            Kind = PowerDeviceKind.Headphones,
            Percent = 85
        },
        new()
        {
            Id = "preview-case",
            Name = "充电盒",
            Kind = PowerDeviceKind.Case,
            Percent = 32,
            Charging = true
        },
        new()
        {
            Id = "preview-keyboard",
            Name = "键盘",
            Kind = PowerDeviceKind.Keyboard,
            Percent = 92
        }
    ];

    private UIElement PowerPreviewCell(PowerDeviceSnapshot device)
    {
        var ring = new ProgressRing(28)
        {
            Clockwise = true,
            Progress = (device.Percent ?? 0) / 100.0,
            Fill = Freeze(device.Percent is <= 15 ? DockTokens.BatteryCritical : DockTokens.BatteryOk),
            Track = Freeze(DockTokens.QuietFill)
        };
        var icon = new TextBlock
        {
            Text = device.Kind switch
            {
                PowerDeviceKind.Laptop => "\uE7F8",
                PowerDeviceKind.Headphones => "\uE7F6",
                PowerDeviceKind.Case => "\uE83F",
                PowerDeviceKind.Keyboard => "\uE92E",
                _ => "\uE83F"
            },
            FontFamily = GlyphFont,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var face = new Grid
        {
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { ring, icon }
        };
        var percent = Label(PowerFormat.Percent(device.Percent), 10, FontWeights.SemiBold);
        percent.HorizontalAlignment = HorizontalAlignment.Center;
        percent.Margin = new Thickness(0, 2, 0, 0);
        return new StackPanel
        {
            Width = 40,
            Margin = new Thickness(4, 0, 4, 0),
            Children = { face, percent }
        };
    }

    private UIElement MediaPreview(MediaSessionSnapshot? session)
    {
        var art = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(DockTokens.ArtCornerRadius),
            Background = Freeze(DockTokens.QuietFill),
            Child = new TextBlock
            {
                Text = "\uE8D6",
                FontFamily = GlyphFont,
                FontSize = 16,
                Foreground = _accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var title = Label(string.IsNullOrWhiteSpace(session?.Title) ? "未在播放" : session!.Title, 14, FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        var artist = Label(session?.Artist ?? string.Empty, 11, FontWeights.Normal);
        artist.Foreground = _mutedBrush;
        return PreviewRow(art, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, artist } });
    }

    private UIElement BingPreview(BingWallpaperSnapshot snapshot)
    {
        var thumb = new Border
        {
            Width = 52,
            Height = 36,
            CornerRadius = new CornerRadius(DockTokens.ArtCornerRadius),
            Background = Freeze(DockTokens.Bing),
            Margin = new Thickness(0, 0, 12, 0)
        };
        if (!string.IsNullOrWhiteSpace(snapshot.LocalPath) && File.Exists(snapshot.LocalPath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 52;
            bitmap.UriSource = new Uri(snapshot.LocalPath);
            bitmap.EndInit();
            bitmap.Freeze();
            thumb.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }

        var title = Label(string.IsNullOrWhiteSpace(snapshot.Selected?.Title) ? "Bing 壁纸" : snapshot.Selected!.Title, 13, FontWeights.SemiBold);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        return PreviewRow(thumb, title);
    }

    private static StackPanel PreviewRow(UIElement left, UIElement right) => new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        Children = { left, right }
    };

    private TextBlock Label(string text, double size, FontWeight weight) => new()
    {
        Text = text,
        FontFamily = UiFont,
        FontSize = size,
        FontWeight = weight,
        Foreground = _textBrush,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void SyncClockTimer()
    {
        _clockTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick -= OnClockTick;
        var run = _catalogActive && IsVisible && IsLoaded && _clockTicks.Count > 0;
        if (!run)
        {
            _clockTimer.Stop();
            return;
        }

        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    private void OnClockTick(object? sender, EventArgs e) => PulseClocks();

    private void PulseClocks()
    {
        if (_session is null || _clockTicks.Count == 0 || !_catalogActive || !IsVisible)
        {
            return;
        }

        var now = _session.Hub.Clock.LocalNow;
        foreach (var tick in _clockTicks)
        {
            tick(now);
        }
    }

    private static SolidColorBrush Freeze(uint argb)
    {
        var (a, r, g, b) = DockTokens.Split(argb);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
