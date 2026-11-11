using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class WhiteNoiseWidget : UserControl,
    IWidgetOpenable,
    IWhiteNoiseConfigurable,
    IKeepAliveWhileRunning,
    IWidgetThemeAware,
    ILocalizable,
    IWidgetDetachable,
    IDockEdgeAware,
    IWidgetModeAware
{
    private const uint Accent = 0xFF0078D4;
    private const double ChipWidth = 92;
    private const double ValWidth = 38;

    private readonly WhiteNoiseAudioEngine _engine = new();
    private readonly Border _tile;
    private readonly Grid _dock = new();
    private readonly StackPanel _textGroup = new() { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
    private readonly TextBlock _tileTitle = WidgetCopy.Primary();
    private readonly TextBlock _tileStatus = WidgetCopy.Secondary();
    private readonly Border _tileQuickToggle;
    private readonly PhosphorGlyph _tileToggleGlyph = new(PhosphorIcon.Play, 12);

    // Dynamic equalizer soundwave bars on tile
    private readonly Rectangle _waveBar1 = WaveBar(4);
    private readonly Rectangle _waveBar2 = WaveBar(7);
    private readonly Rectangle _waveBar3 = WaveBar(10);
    private readonly Rectangle _waveBar4 = WaveBar(5);
    private readonly StackPanel _wavePanel;
    private readonly DispatcherTimer _waveTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Random _waveRng = new();

    // Flyout elements
    private readonly Popup _popup;
    private readonly Border _flyoutCard;
    private readonly StackPanel _flyoutContent = new();
    private readonly ScrollViewer _mixerScroll;
    private readonly StackPanel _mixerTrackList = new();
    private TextBlock _flyoutTitle = null!;
    private TextBlock _presetsHeader = null!;
    private TextBlock _mixerHeader = null!;
    private TextBlock _timerHeader = null!;
    private readonly Border _primaryPlayBtn;
    private readonly TextBlock _primaryPlayText = WidgetCopy.Primary();
    private readonly PhosphorGlyph _primaryPlayIcon = new(PhosphorIcon.Play, 13);
    private readonly Slider _masterSlider = new() { Minimum = 0, Maximum = 100, IsMoveToPointEnabled = true, Height = 22, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _masterVolText = WidgetCopy.Secondary();
    private readonly WrapPanel _presetChipsHost = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
    private readonly WrapPanel _sleepChipsHost = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
    private readonly CheckBox _linkFocusCheck = new() { Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _fadeCheck = new() { Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center };

    private readonly List<(string PresetId, Border Chip, TextBlock Label)> _presetChips = [];
    private readonly List<(int Minutes, Border Chip, TextBlock Label)> _sleepChips = [];
    private readonly Dictionary<string, (Slider Slider, TextBlock ValText, Border ToggleChip, TextBlock NameLabel)> _trackControls = new(StringComparer.Ordinal);

    private bool _applying;
    private bool _side;
    private bool _isLight;
    private WidgetDensity _density = WidgetDensity.Expanded;

    static WhiteNoiseWidget()
    {
        ProceduralAudioGenerator.WarmUpInBackground();
    }

    public WhiteNoiseWidget()
    {
        _tileQuickToggle = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = TokenBrushes.From(0x22000000),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _tileToggleGlyph
        };
        _tileQuickToggle.MouseLeftButtonUp += (_, e) =>
        {
            _engine.Toggle();
            Persist();
            e.Handled = true;
        };

        _wavePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 18,
            Height = 16,
            ClipToBounds = true,
            Children = { _waveBar1, _waveBar2, _waveBar3, _waveBar4 }
        };

        _tileStatus.Visibility = Visibility.Collapsed;
        _textGroup.Children.Add(_tileTitle);
        _textGroup.Children.Add(_tileStatus);

        _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_wavePanel, 0);
        Grid.SetColumn(_textGroup, 1);
        Grid.SetColumn(_tileQuickToggle, 2);

        _dock.Children.Add(_wavePanel);
        _dock.Children.Add(_textGroup);
        _dock.Children.Add(_tileQuickToggle);

        _tile = WidgetChrome.Wrap(_dock, minWidth: 64);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            if (_popup?.IsOpen == true)
            {
                _popup.IsOpen = false;
            }
            else
            {
                Open();
            }

            e.Handled = true;
        };

        _waveTimer.Tick += OnWaveTimerTick;

        // Primary Play/Pause Button in Master Controls Row
        _primaryPlayIcon.VerticalAlignment = VerticalAlignment.Center;
        _primaryPlayIcon.Margin = new Thickness(0, 0, 5, 0);
        _primaryPlayText.FontSize = 12;
        _primaryPlayText.FontWeight = FontWeights.SemiBold;
        _primaryPlayText.VerticalAlignment = VerticalAlignment.Center;
        var playBtnContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _primaryPlayIcon, _primaryPlayText }
        };

        _primaryPlayBtn = new Border
        {
            Width = ChipWidth,
            Height = 28,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Cursor = Cursors.Hand,
            Child = playBtnContent
        };
        _primaryPlayBtn.MouseLeftButtonUp += (_, e) =>
        {
            _engine.Toggle();
            Persist();
            e.Handled = true;
        };

        _mixerScroll = new ScrollViewer
        {
            MaxHeight = 210,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _mixerTrackList
        };

        _mixerScroll.PreviewMouseWheel += (_, e) =>
        {
            if (_mixerScroll.ScrollableHeight > 0)
            {
                _mixerScroll.ScrollToVerticalOffset(_mixerScroll.VerticalOffset - (e.Delta * 0.4));
                e.Handled = true;
            }
        };

        BuildFlyoutStructure();

        _flyoutCard = WidgetChrome.Flyout(_flyoutContent, minWidth: 350);
        _flyoutCard.RenderTransformOrigin = new Point(0.5, 0);
        _flyoutCard.RenderTransform = new ScaleTransform(1, 1);

        _flyoutCard.PreviewMouseWheel += (_, e) =>
        {
            if (!e.Handled && _mixerScroll.ScrollableHeight > 0)
            {
                _mixerScroll.ScrollToVerticalOffset(_mixerScroll.VerticalOffset - (e.Delta * 0.4));
                e.Handled = true;
            }
        };

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8,
            Child = _flyoutCard
        };
        WidgetFlyout.Attach(_popup);
        _popup.Opened += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
            WidgetFlyout.Wheel += OnFlyoutWheel;
            var scale = (ScaleTransform)_flyoutCard.RenderTransform;
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _flyoutCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        };
        _popup.Closed += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
        };

        _engine.PlaybackStateChanged += (_, _) => Render();
        _engine.SleepTimerTick += (_, _) => Render();

        Loaded += (_, _) => Render();
        Unloaded += (_, _) =>
        {
            WidgetFlyout.Wheel -= OnFlyoutWheel;
            _waveTimer.Stop();
            _popup.IsOpen = false;
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        ApplyTheme(false);
        ApplyLocale();
        Render();
    }

    public bool IsAlive => _engine.IsPlaying;

    public event EventHandler<WhiteNoiseSettings>? SettingsChanged;

    public event EventHandler<string>? PaperChanged
    {
        add { }
        remove { }
    }

    public void Open()
    {
        _popup.IsOpen = true;
    }

    public void ApplyPaper(string? paper)
    {
    }

    public void ApplySettings(WhiteNoiseSettings settings)
    {
        _applying = true;
        try
        {
            _engine.ApplySettings(settings);
            SyncFlyoutControls();
            Render();
        }
        finally
        {
            _applying = false;
        }
    }

    public void ApplyDensity(WidgetDensity density)
    {
        _density = density;
        Render();
    }

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        WidgetChrome.FitEdge(_tile, _side, 70);
        _dock.RowDefinitions.Clear();
        _dock.ColumnDefinitions.Clear();
        if (_side)
        {
            _dock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _dock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _dock.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(_wavePanel, 0);
            Grid.SetColumn(_wavePanel, 0);
            Grid.SetRow(_textGroup, 1);
            Grid.SetColumn(_textGroup, 0);
            Grid.SetRow(_tileQuickToggle, 2);
            Grid.SetColumn(_tileQuickToggle, 0);

            _wavePanel.Margin = new Thickness(0, 0, 0, 2);
            _textGroup.Margin = new Thickness(0, 2, 0, 2);
            _textGroup.HorizontalAlignment = HorizontalAlignment.Center;
            _tileTitle.HorizontalAlignment = HorizontalAlignment.Center;
            _tileTitle.TextAlignment = TextAlignment.Center;
            _tileTitle.MaxWidth = DockTokens.SideTileWidth - 12;
            _tileTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            _tileStatus.HorizontalAlignment = HorizontalAlignment.Center;
            _tileStatus.TextAlignment = TextAlignment.Center;

            _tileQuickToggle.Margin = new Thickness(0, 2, 0, 0);
            _tileQuickToggle.HorizontalAlignment = HorizontalAlignment.Center;
            _tileQuickToggle.Visibility = Visibility.Visible;
        }
        else
        {
            _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _dock.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(_wavePanel, 0);
            Grid.SetRow(_wavePanel, 0);
            Grid.SetColumn(_textGroup, 1);
            Grid.SetRow(_textGroup, 0);
            Grid.SetColumn(_tileQuickToggle, 2);
            Grid.SetRow(_tileQuickToggle, 0);

            _wavePanel.Margin = new Thickness(0);
            _textGroup.Margin = new Thickness(6, 0, 6, 0);
            _textGroup.HorizontalAlignment = HorizontalAlignment.Left;
            _tileTitle.HorizontalAlignment = HorizontalAlignment.Left;
            _tileTitle.TextAlignment = TextAlignment.Left;
            _tileTitle.MaxWidth = 110;
            _tileTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            _tileStatus.HorizontalAlignment = HorizontalAlignment.Left;
            _tileStatus.TextAlignment = TextAlignment.Left;

            _tileQuickToggle.Margin = new Thickness(0, 0, 0, 0);
            _tileQuickToggle.HorizontalAlignment = HorizontalAlignment.Center;
            _tileQuickToggle.Visibility = Visibility.Visible;
        }
    }

    public void ApplyTheme(bool isLight)
    {
        _isLight = isLight;
        WidgetChrome.ApplyTheme(_tile, isLight);
        WidgetChrome.ApplyTheme(_flyoutCard, isLight);
        WidgetChrome.PaintScrollViewer(_mixerScroll, isLight);
        WidgetChrome.ApplyTree(_flyoutContent, isLight, WidgetChrome.ThemeBackdrop);

        var sliderStyle = CreateSliderStyle(isLight, Accent);
        _masterSlider.Style = sliderStyle;
        foreach (var ctrl in _trackControls.Values)
        {
            ctrl.Slider.Style = sliderStyle;
        }

        var checkStyle = CreateCheckBoxStyle(isLight, Accent);
        _linkFocusCheck.Style = checkStyle;
        _fadeCheck.Style = checkStyle;

        var accentBrush = TokenBrushes.From(Accent);
        _waveBar1.Fill = accentBrush;
        _waveBar2.Fill = accentBrush;
        _waveBar3.Fill = accentBrush;
        _waveBar4.Fill = accentBrush;

        SyncFlyoutControls();
        Render();
    }

    public void ApplyLocale()
    {
        _flyoutTitle.Text = UiText.WidgetWhiteNoise;
        _presetsHeader.Text = UiText.WhiteNoisePresetsSection;
        _mixerHeader.Text = UiText.WhiteNoiseMixerSection;
        _timerHeader.Text = UiText.WhiteNoiseSleepTimerSection;
        _linkFocusCheck.Content = UiText.WhiteNoiseLinkFocus;
        _fadeCheck.Content = UiText.WhiteNoiseFade;

        foreach (var (presetId, _, label) in _presetChips)
        {
            label.Text = WhiteNoisePresets.DisplayName(presetId);
        }

        foreach (var (minutes, _, label) in _sleepChips)
        {
            label.Text = minutes == 0 ? UiText.WhiteNoiseTimerOff : UiText.WhiteNoiseTimerMin(minutes);
        }

        foreach (var (trackId, ctrl) in _trackControls)
        {
            ctrl.NameLabel.Text = WhiteNoiseTracks.DisplayName(trackId);
        }

        Render();
    }

    public void Detach()
    {
        WidgetFlyout.Wheel -= OnFlyoutWheel;
        _waveTimer.Stop();
        _engine.Dispose();
        _popup.IsOpen = false;
    }

    private void OnFlyoutWheel(object? sender, FlyoutWheelEventArgs e)
    {
        if (_popup?.IsOpen == true && _mixerScroll.ScrollableHeight > 0)
        {
            _mixerScroll.ScrollToVerticalOffset(_mixerScroll.VerticalOffset - (e.Delta * 0.4));
        }
    }

    private void BuildFlyoutStructure()
    {
        _flyoutTitle = HeaderLabel(UiText.WidgetWhiteNoise);
        var closeBtn = WidgetChrome.HeaderClose(() => _popup.IsOpen = false, 18);
        var header = HeaderRow(_flyoutTitle, closeBtn);

        // Master controls row: Master Play button + Master volume slider + Percentage
        _masterSlider.Margin = new Thickness(10, 0, 10, 0);
        _masterSlider.ValueChanged += (_, _) =>
        {
            if (!_applying)
            {
                _engine.SetMasterVolume(_masterSlider.Value / 100.0);
                _masterVolText.Text = $"{Math.Round(_masterSlider.Value)}%";
                Persist();
            }
        };

        _masterVolText.Width = ValWidth;
        _masterVolText.FontSize = 12;
        _masterVolText.TextAlignment = TextAlignment.Right;
        _masterVolText.VerticalAlignment = VerticalAlignment.Center;

        var masterControls = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        masterControls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ChipWidth) });
        masterControls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        masterControls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ValWidth) });

        Grid.SetColumn(_primaryPlayBtn, 0);
        Grid.SetColumn(_masterSlider, 1);
        Grid.SetColumn(_masterVolText, 2);

        masterControls.Children.Add(_primaryPlayBtn);
        masterControls.Children.Add(_masterSlider);
        masterControls.Children.Add(_masterVolText);

        // Presets Section
        _presetsHeader = SectionLabel(UiText.WhiteNoisePresetsSection);
        foreach (var preset in WhiteNoisePresets.All)
        {
            var pChip = CreatePresetChip(preset);
            _presetChips.Add(pChip);
            _presetChipsHost.Children.Add(pChip.Chip);
        }

        // Track Mixer Section
        _mixerHeader = SectionLabel(UiText.WhiteNoiseMixerSection);
        foreach (var track in WhiteNoiseTracks.All)
        {
            var row = CreateTrackRow(track);
            _mixerTrackList.Children.Add(row);
        }

        // Sleep Timer Section
        _timerHeader = SectionLabel(UiText.WhiteNoiseSleepTimerSection);
        int[] timerOptions = [0, 15, 30, 45, 60];
        foreach (var opt in timerOptions)
        {
            var sChip = CreateSleepChip(opt);
            _sleepChips.Add(sChip);
            _sleepChipsHost.Children.Add(sChip.Chip);
        }

        // Options Checkboxes
        _linkFocusCheck.Checked += (_, _) => { if (!_applying) { _engine.ApplySettings(_engine.Settings with { LinkWithFocusTimer = true }); Persist(); } };
        _linkFocusCheck.Unchecked += (_, _) => { if (!_applying) { _engine.ApplySettings(_engine.Settings with { LinkWithFocusTimer = false }); Persist(); } };
        _fadeCheck.Checked += (_, _) => { if (!_applying) { _engine.ApplySettings(_engine.Settings with { FadeInOut = true }); Persist(); } };
        _fadeCheck.Unchecked += (_, _) => { if (!_applying) { _engine.ApplySettings(_engine.Settings with { FadeInOut = false }); Persist(); } };

        _flyoutContent.Children.Add(header);
        _flyoutContent.Children.Add(masterControls);
        _flyoutContent.Children.Add(CreateDivider());
        _flyoutContent.Children.Add(_presetsHeader);
        _flyoutContent.Children.Add(_presetChipsHost);
        _flyoutContent.Children.Add(CreateDivider());
        _flyoutContent.Children.Add(_mixerHeader);
        _flyoutContent.Children.Add(_mixerScroll);
        _flyoutContent.Children.Add(CreateDivider());
        _flyoutContent.Children.Add(_timerHeader);
        _flyoutContent.Children.Add(_sleepChipsHost);
        _flyoutContent.Children.Add(_linkFocusCheck);
        _flyoutContent.Children.Add(_fadeCheck);
    }

    private (string PresetId, Border Chip, TextBlock Label) CreatePresetChip(string presetId)
    {
        var label = WidgetCopy.Primary();
        label.Text = WhiteNoisePresets.DisplayName(presetId);
        label.FontSize = 12;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.IsHitTestVisible = false;

        var chip = new Border
        {
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 6),
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Cursor = Cursors.Hand,
            Child = label
        };
        WidgetChrome.MarkQuiet(chip);

        chip.MouseLeftButtonUp += (_, e) =>
        {
            _engine.ApplyPreset(presetId);
            SyncFlyoutControls();
            Persist();
            e.Handled = true;
        };

        return (presetId, chip, label);
    }

    private (int Minutes, Border Chip, TextBlock Label) CreateSleepChip(int minutes)
    {
        var label = WidgetCopy.Primary();
        label.Text = minutes == 0 ? UiText.WhiteNoiseTimerOff : UiText.WhiteNoiseTimerMin(minutes);
        label.FontSize = 12;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.IsHitTestVisible = false;

        var chip = new Border
        {
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 6),
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Cursor = Cursors.Hand,
            Child = label
        };
        WidgetChrome.MarkQuiet(chip);

        chip.MouseLeftButtonUp += (_, e) =>
        {
            _engine.SetSleepTimer(minutes);
            SyncSleepChips();
            Persist();
            e.Handled = true;
        };

        return (minutes, chip, label);
    }

    private UIElement CreateTrackRow(string trackId)
    {
        var nameLabel = WidgetCopy.Primary();
        nameLabel.Text = WhiteNoiseTracks.DisplayName(trackId);
        nameLabel.FontSize = 12;
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.VerticalAlignment = VerticalAlignment.Center;
        nameLabel.TextAlignment = TextAlignment.Center;
        nameLabel.IsHitTestVisible = false;

        var toggleChip = new Border
        {
            Width = ChipWidth,
            Height = 28,
            CornerRadius = new CornerRadius(DockTokens.TileCornerRadius),
            Cursor = Cursors.Hand,
            Child = nameLabel
        };
        WidgetChrome.MarkQuiet(toggleChip);

        toggleChip.MouseLeftButtonUp += (_, e) =>
        {
            _engine.ToggleTrack(trackId);
            SyncFlyoutControls();
            Persist();
            e.Handled = true;
        };

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            IsMoveToPointEnabled = true,
            Height = 22,
            Margin = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var valText = WidgetCopy.Secondary();
        valText.Width = ValWidth;
        valText.FontSize = 12;
        valText.TextAlignment = TextAlignment.Right;
        valText.VerticalAlignment = VerticalAlignment.Center;

        slider.ValueChanged += (_, _) =>
        {
            if (!_applying)
            {
                var vol = slider.Value / 100.0;
                _engine.SetTrackVolume(trackId, vol);
                valText.Text = $"{Math.Round(slider.Value)}%";
                SyncFlyoutControls();
                Persist();
            }
        };

        _trackControls[trackId] = (slider, valText, toggleChip, nameLabel);

        var rowGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ChipWidth) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ValWidth) });

        Grid.SetColumn(toggleChip, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(valText, 2);

        rowGrid.Children.Add(toggleChip);
        rowGrid.Children.Add(slider);
        rowGrid.Children.Add(valText);

        return rowGrid;
    }

    private void SyncFlyoutControls()
    {
        _applying = true;
        try
        {
            var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);

            _masterSlider.Value = _engine.Settings.MasterVolume * 100.0;
            _masterVolText.Text = $"{Math.Round(_masterSlider.Value)}%";
            _linkFocusCheck.IsChecked = _engine.Settings.LinkWithFocusTimer;
            _fadeCheck.IsChecked = _engine.Settings.FadeInOut;

            // Sync preset chips
            foreach (var (presetId, chip, label) in _presetChips)
            {
                var isSelected = string.Equals(presetId, _engine.Settings.ActivePreset, StringComparison.Ordinal);
                label.Tag = isSelected ? "tint" : null;
                if (isSelected)
                {
                    WidgetChrome.ClearQuiet(chip, Accent);
                    label.Foreground = Brushes.White;
                    label.FontWeight = FontWeights.SemiBold;
                    label.Opacity = 1.0;
                }
                else
                {
                    WidgetChrome.MarkQuiet(chip);
                    label.Foreground = textBrush;
                    label.FontWeight = FontWeights.Normal;
                    label.Opacity = 0.75;
                }
            }

            // Sync all track rows in the mixer
            foreach (var (trackId, ctrl) in _trackControls)
            {
                var track = _engine.Settings.Tracks.FirstOrDefault(t => string.Equals(t.Id, trackId, StringComparison.Ordinal));
                var volume = track?.Volume ?? 0.5;
                var isEnabled = track?.Enabled == true;

                ctrl.Slider.Value = volume * 100.0;
                ctrl.ValText.Text = $"{Math.Round(ctrl.Slider.Value)}%";

                var isActive = isEnabled && volume > 0.01;
                ctrl.ToggleChip.Tag = isActive ? "tint" : null;
                if (isActive)
                {
                    WidgetChrome.ClearQuiet(ctrl.ToggleChip, Accent);
                    ctrl.NameLabel.Foreground = Brushes.White;
                    ctrl.NameLabel.FontWeight = FontWeights.SemiBold;
                    ctrl.NameLabel.Opacity = 1.0;
                    ctrl.ValText.Opacity = 1.0;
                }
                else
                {
                    WidgetChrome.MarkQuiet(ctrl.ToggleChip);
                    ctrl.NameLabel.Foreground = textBrush;
                    ctrl.NameLabel.FontWeight = FontWeights.Normal;
                    ctrl.NameLabel.Opacity = 0.65;
                    ctrl.ValText.Opacity = 0.65;
                }
            }

            SyncSleepChips();
        }
        finally
        {
            _applying = false;
        }

        Render();
    }

    private void SyncSleepChips()
    {
        var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);
        foreach (var (minutes, chip, label) in _sleepChips)
        {
            var isSelected = minutes == _engine.Settings.TimerMinutes;
            label.Tag = isSelected ? "tint" : null;
            if (isSelected)
            {
                WidgetChrome.ClearQuiet(chip, Accent);
                label.Foreground = Brushes.White;
                label.FontWeight = FontWeights.SemiBold;
                label.Opacity = 1.0;
            }
            else
            {
                WidgetChrome.MarkQuiet(chip);
                label.Foreground = textBrush;
                label.FontWeight = FontWeights.Normal;
                label.Opacity = 0.75;
            }
        }
    }

    private void Render()
    {
        var isPlaying = _engine.IsPlaying;
        var textBrush = TokenBrushes.From(_isLight ? DockTokens.Light.Text : DockTokens.Dark.Text);

        if (isPlaying)
        {
            _waveTimer.Start();
            _tileToggleGlyph.Icon = PhosphorIcon.Pause;
            _tileToggleGlyph.Foreground = Brushes.White;
            _tileQuickToggle.Background = TokenBrushes.From(Accent);
            _tileQuickToggle.ToolTip = UiText.Pause;

            _primaryPlayIcon.Icon = PhosphorIcon.Pause;
            _primaryPlayText.Text = UiText.Pause;
            _primaryPlayBtn.Tag = "tint";
            _primaryPlayBtn.Background = TokenBrushes.From(Accent);
            _primaryPlayIcon.Foreground = Brushes.White;
            _primaryPlayText.Foreground = Brushes.White;
        }
        else
        {
            _waveTimer.Stop();
            _waveBar1.Height = 4;
            _waveBar2.Height = 4;
            _waveBar3.Height = 4;
            _waveBar4.Height = 4;

            _tileToggleGlyph.Icon = PhosphorIcon.Play;
            _tileToggleGlyph.Foreground = textBrush;
            _tileQuickToggle.Background = TokenBrushes.From(_isLight ? 0x14000000u : 0x22FFFFFFu);
            _tileQuickToggle.ToolTip = UiText.Play;

            _primaryPlayIcon.Icon = PhosphorIcon.Play;
            _primaryPlayText.Text = UiText.Play;
            _primaryPlayBtn.Tag = null;
            WidgetChrome.MarkQuiet(_primaryPlayBtn);
            _primaryPlayIcon.Foreground = textBrush;
            _primaryPlayText.Foreground = textBrush;
        }

        _tileTitle.Text = WhiteNoisePresets.DisplayName(_engine.Settings.ActivePreset);

        var remaining = _engine.RemainingSleepTime;
        if (remaining > TimeSpan.Zero)
        {
            var min = (int)Math.Ceiling(remaining.TotalMinutes);
            _tileStatus.Text = $"{min}m";
            _tileStatus.Visibility = Visibility.Visible;
        }
        else
        {
            _tileStatus.Text = string.Empty;
            _tileStatus.Visibility = Visibility.Collapsed;
        }

        // Density adjustments
        if (_density == WidgetDensity.Compact && !_side)
        {
            _tileTitle.Visibility = Visibility.Collapsed;
            _wavePanel.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _tileTitle.Visibility = Visibility.Visible;
            _wavePanel.Visibility = Visibility.Visible;
        }
    }

    private void OnWaveTimerTick(object? sender, EventArgs e)
    {
        if (_engine.IsPlaying)
        {
            _waveBar1.Height = _waveRng.Next(3, 11);
            _waveBar2.Height = _waveRng.Next(5, 14);
            _waveBar3.Height = _waveRng.Next(4, 15);
            _waveBar4.Height = _waveRng.Next(3, 10);
        }
    }

    private void Persist()
    {
        if (!_applying)
        {
            SettingsChanged?.Invoke(this, _engine.Settings);
        }
    }

    private static Rectangle WaveBar(double height) => new()
    {
        Width = 2.5,
        Height = height,
        RadiusX = 1,
        RadiusY = 1,
        Margin = new Thickness(1, 0, 1, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Fill = TokenBrushes.From(Accent)
    };

    private static TextBlock HeaderLabel(string text) => new()
    {
        Text = text,
        FontFamily = WidgetFonts.Ui,
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontFamily = WidgetFonts.Ui,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Opacity = 0.65,
        Margin = new Thickness(0, 8, 0, 4)
    };

    private static Border CreateDivider() => new()
    {
        Height = 1,
        Margin = new Thickness(0, 8, 0, 8),
        Background = TokenBrushes.From(0x18888888),
        Opacity = 0.4
    };

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
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

    private static Style CreateSliderStyle(bool isLight, uint accent)
    {
        var accentHex = $"#{accent:X8}";
        var trackQuietHex = isLight ? "#24000000" : "#2AFFFFFF";
        var thumbStrokeHex = isLight ? "#B0B0B0" : accentHex;
        var thumbFillHex = "#FFFFFF";

        var xaml = $$"""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   TargetType="Slider">
                <Setter Property="Height" Value="22"/>
                <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                <Setter Property="SnapsToDevicePixels" Value="True"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Slider">
                            <Grid VerticalAlignment="Center">
                                <Track x:Name="PART_Track">
                                    <Track.DecreaseRepeatButton>
                                        <RepeatButton Command="Slider.DecreaseLarge" Focusable="False">
                                            <RepeatButton.Template>
                                                <ControlTemplate TargetType="RepeatButton">
                                                    <Border Height="4" VerticalAlignment="Center" Background="{{accentHex}}" CornerRadius="2"/>
                                                </ControlTemplate>
                                            </RepeatButton.Template>
                                        </RepeatButton>
                                    </Track.DecreaseRepeatButton>
                                    <Track.Thumb>
                                        <Thumb Width="14" Height="14" FocusVisualStyle="{x:Null}" Cursor="Hand">
                                            <Thumb.Template>
                                                <ControlTemplate TargetType="Thumb">
                                                    <Ellipse Fill="{{thumbFillHex}}" Stroke="{{thumbStrokeHex}}" StrokeThickness="2"/>
                                                </ControlTemplate>
                                            </Thumb.Template>
                                        </Thumb>
                                    </Track.Thumb>
                                    <Track.IncreaseRepeatButton>
                                        <RepeatButton Command="Slider.IncreaseLarge" Focusable="False">
                                            <RepeatButton.Template>
                                                <ControlTemplate TargetType="RepeatButton">
                                                    <Border Height="4" VerticalAlignment="Center" Background="{{trackQuietHex}}" CornerRadius="2"/>
                                                </ControlTemplate>
                                            </RepeatButton.Template>
                                        </RepeatButton>
                                    </Track.IncreaseRepeatButton>
                                </Track>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
            """;
        return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private static Style CreateCheckBoxStyle(bool isLight, uint accent)
    {
        var accentHex = $"#{accent:X8}";
        var bgHex = isLight ? "#14000000" : "#22FFFFFF";
        var borderHex = isLight ? "#28000000" : "#44FFFFFF";
        var textHex = isLight ? "#1A1A1A" : "#FFFFFF";

        var xaml = $$"""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                   xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                   TargetType="CheckBox">
                <Setter Property="FontSize" Value="12"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Foreground" Value="{{textHex}}"/>
                <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                <Setter Property="SnapsToDevicePixels" Value="True"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="CheckBox">
                            <StackPanel Orientation="Horizontal" Background="Transparent">
                                <Border x:Name="Box"
                                        Width="16"
                                        Height="16"
                                        Margin="0,0,8,0"
                                        VerticalAlignment="Center"
                                        Background="{{bgHex}}"
                                        BorderBrush="{{borderHex}}"
                                        BorderThickness="1"
                                        CornerRadius="4">
                                    <Path x:Name="Mark"
                                          Data="M 3 8 L 6 11 L 13 4"
                                          Stroke="White"
                                          StrokeThickness="1.8"
                                          StrokeStartLineCap="Round"
                                          StrokeEndLineCap="Round"
                                          HorizontalAlignment="Center"
                                          VerticalAlignment="Center"
                                          Visibility="Collapsed"/>
                                </Border>
                                <ContentPresenter VerticalAlignment="Center"/>
                            </StackPanel>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsChecked" Value="True">
                                    <Setter TargetName="Box" Property="Background" Value="{{accentHex}}"/>
                                    <Setter TargetName="Box" Property="BorderBrush" Value="{{accentHex}}"/>
                                    <Setter TargetName="Mark" Property="Visibility" Value="Visible"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
            """;
        return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}
