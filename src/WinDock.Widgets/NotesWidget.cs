using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinDock.Core;

namespace WinDock.Widgets;

public sealed class NotesWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, INotesHistory
{
    private readonly INotesStore _store;
    private readonly string _key;
    private readonly TextBlock _preview;
    private readonly TextBox _box;
    private readonly Border _tile;
    private readonly Border _panel;
    private readonly Grid _editorPage;
    private readonly Grid _customizePage;
    private readonly StackPanel _swatches = new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 10, 0, 0)
    };
    private readonly StackPanel _historyList = new();
    private readonly TextBlock _historyCaption = new();
    private readonly ScrollViewer _historyScroll = new()
    {
        MaxHeight = 148,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Margin = new Thickness(0, 8, 0, 0)
    };
    private readonly List<(string Id, TextBlock Check)> _marks = [];
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly Popup _popup;
    private FrameworkElement _deleteButton = null!;
    private FrameworkElement _deleteConfirm = null!;
    private string _paper = NotePapers.Default;
    private string _committed = string.Empty;
    private bool _discarded;

    public NotesWidget(INotesStore store, string key)
    {
        _store = store;
        _key = key;
        var ink = TokenBrushes.From(NotePapers.Ink);
        _preview = WidgetCopy.Primary();
        _preview.Tag = "ink";
        _preview.FontFamily = WidgetFonts.Notes;
        _preview.FontWeight = FontWeights.Bold;
        _preview.FontSize = DockTokens.SecondaryFontSize;
        _preview.Foreground = ink;
        _preview.TextWrapping = TextWrapping.Wrap;
        _preview.TextTrimming = TextTrimming.CharacterEllipsis;
        _preview.TextAlignment = TextAlignment.Left;
        _preview.Width = 132;
        _preview.MaxHeight = 28;
        _preview.LineHeight = 14;
        _preview.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _preview.Text = PreviewText(store.Load(_key));
        FitPreview(store.Load(_key));

        _box = new TextBox
        {
            Tag = "ink",
            Text = store.Load(_key),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            CaretBrush = ink,
            Foreground = ink,
            FontFamily = WidgetFonts.Notes,
            FontSize = 16,
            MinHeight = 148
        };

        _tile = WidgetChrome.Wrap(_preview, 0, WidgetChrome.NotesTag);
        _tile.MouseLeftButtonUp += (_, e) =>
        {
            Open();
            e.Handled = true;
        };

        _popup = new Popup
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = _tile,
            StaysOpen = false,
            AllowsTransparency = true,
            VerticalOffset = 8
        };
        WidgetFlyout.Attach(_popup);
        _editorPage = BuildEditor(ink);
        _customizePage = BuildCustomize(ink);
        _customizePage.Visibility = Visibility.Collapsed;

        var body = new Grid();
        body.Children.Add(_editorPage);
        body.Children.Add(_customizePage);

        _panel = new Border
        {
            Tag = WidgetChrome.NotesTag,
            CornerRadius = new CornerRadius(DockTokens.IslandCornerRadius),
            Padding = new Thickness(16, 12, 16, 16),
            MinWidth = 300,
            MinHeight = 220,
            SnapsToDevicePixels = true,
            Child = body
        };
        WidgetChrome.Round(_panel, DockTokens.IslandCornerRadius);
        _popup.Child = WidgetChrome.Shadow(_panel);
        _popup.Closed += (_, _) =>
        {
            Save();
            ShowEditor();
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Save();
        };
        _box.TextChanged += (_, _) =>
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        };
        _committed = _box.Text;
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Save();
                e.Handled = true;
            }
        };

        Unloaded += (_, _) =>
        {
            _saveTimer.Stop();
            ClosePopup();
            _saveTimer.Stop();
            Save();
        };

        var root = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        root.Children.Add(_tile);
        root.Children.Add(_popup);
        Content = root;
        Paint();
    }

    public event EventHandler<string>? PaperChanged;

    public event EventHandler? DeleteRequested;

    public event EventHandler<string>? RestoreRequested;

    public event EventHandler<string>? DiscardRequested;

    public void ApplyHistory(IReadOnlyList<NotesHistoryItem> items)
    {
        _historyList.Children.Clear();
        var any = items.Count > 0;
        _historyCaption.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        _historyScroll.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in items)
        {
            _historyList.Children.Add(HistoryRow(item));
        }
    }

    public void Flush()
    {
        _saveTimer.Stop();
        Save();
    }

    public void Discard()
    {
        _discarded = true;
        _saveTimer.Stop();
        ClosePopup();
    }

    public void Open()
    {
        _box.Text = _store.Load(_key);
        _committed = _box.Text;
        ShowEditor();
        _popup.IsOpen = true;
        _box.Focus();
    }

    public void OpenCustomize()
    {
        Flush();
        ShowCustomize();
        _popup.IsOpen = true;
    }

    public void ApplyPaper(string? paper)
    {
        _paper = NotePapers.Normalize(paper);
        Paint();
    }

    private void ShowEditor()
    {
        HideDeleteConfirm();
        _popup.StaysOpen = false;
        if (NotesText.IsBlank(_box.Text) && !NotesText.IsBlank(_committed))
        {
            _box.Text = _committed;
        }

        _editorPage.Visibility = Visibility.Visible;
        _customizePage.Visibility = Visibility.Collapsed;
    }

    private void ShowCustomize()
    {
        HideDeleteConfirm();
        _popup.StaysOpen = true;
        _historyScroll.IsHitTestVisible = false;
        _editorPage.Visibility = Visibility.Collapsed;
        _customizePage.Visibility = Visibility.Visible;
        RefreshSwatches();
        Dispatcher.BeginInvoke(() => _historyScroll.IsHitTestVisible = true, DispatcherPriority.Background);
    }

    private void ClosePopup()
    {
        HideDeleteConfirm();
        _popup.StaysOpen = false;
        _popup.IsOpen = false;
    }

    private void Choose(string paper)
    {
        var next = NotePapers.Normalize(paper);
        if (string.Equals(next, _paper, StringComparison.Ordinal))
        {
            return;
        }

        _paper = next;
        Paint();
        PaperChanged?.Invoke(this, _paper);
    }

    private void Paint()
    {
        var fill = TokenBrushes.From(NotePapers.Fill(_paper));
        var ink = TokenBrushes.From(NotePapers.Ink);
        _tile.Background = fill;
        _panel.Background = fill;
        TextElement.SetForeground(_tile, ink);
        TextElement.SetForeground(_panel, ink);
        _preview.Foreground = ink;
        _box.Foreground = ink;
        _box.CaretBrush = ink;
        RefreshSwatches();
    }

    private Grid BuildEditor(Brush ink)
    {
        var title = HeaderLabel("便笺", ink);
        var customize = HeaderGlyph("\uE8FD", ink, OpenCustomize);
        customize.ToolTip = "自定义";
        var close = HeaderGlyph("\u00D7", ink, ClosePopup, 18);
        var header = HeaderRow(title, customize, close);
        var footer = BuildDeleteFooter(ink);
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.Children.Add(header);
        Grid.SetRow(_box, 1);
        page.Children.Add(_box);
        Grid.SetRow(footer, 2);
        page.Children.Add(footer);
        return page;
    }

    private Grid BuildCustomize(Brush ink)
    {
        var back = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Children =
            {
                HeaderGlyph("\uE76B", ink, ShowEditor, 12),
                HeaderLabel("返回", ink)
            }
        };
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowEditor();
            e.Handled = true;
        };
        var title = HeaderLabel("自定义", ink);
        title.Margin = new Thickness(8, 0, 0, 0);
        title.FontWeight = FontWeights.SemiBold;
        var close = HeaderGlyph("\u00D7", ink, ClosePopup, 18);
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { back, title }
        };
        var header = HeaderRow(left, close);
        var caption = HeaderLabel("纸张颜色", ink);
        caption.Opacity = 0.62;
        caption.Margin = new Thickness(0, 14, 0, 0);
        foreach (var id in NotePapers.All)
        {
            var (dot, check) = Swatch(id);
            _swatches.Children.Add(dot);
            _marks.Add((id, check));
        }

        _historyCaption.Tag = "ink";
        _historyCaption.Text = "历史便笺";
        _historyCaption.FontFamily = WidgetFonts.Notes;
        _historyCaption.FontSize = 13;
        _historyCaption.Foreground = ink;
        _historyCaption.Opacity = 0.62;
        _historyCaption.Margin = new Thickness(0, 16, 0, 0);
        _historyCaption.Visibility = Visibility.Collapsed;
        _historyScroll.Content = _historyList;
        _historyScroll.Visibility = Visibility.Collapsed;

        var syntaxCaption = HeaderLabel("便签语法", ink);
        syntaxCaption.Opacity = 0.62;
        syntaxCaption.Margin = new Thickness(0, 16, 0, 0);
        var syntaxHint = HeaderLabel("第一行以 ### 开头作为标题。有标题时 Dock 只显示标题；没有标题时显示前两行。", ink);
        syntaxHint.TextWrapping = TextWrapping.Wrap;
        syntaxHint.Opacity = 0.78;
        syntaxHint.Margin = new Thickness(0, 6, 0, 0);
        syntaxHint.MaxWidth = 268;

        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.Children.Add(header);
        Grid.SetRow(caption, 1);
        page.Children.Add(caption);
        Grid.SetRow(_swatches, 2);
        page.Children.Add(_swatches);
        Grid.SetRow(syntaxCaption, 3);
        page.Children.Add(syntaxCaption);
        Grid.SetRow(syntaxHint, 4);
        page.Children.Add(syntaxHint);
        Grid.SetRow(_historyCaption, 5);
        page.Children.Add(_historyCaption);
        Grid.SetRow(_historyScroll, 6);
        page.Children.Add(_historyScroll);
        return page;
    }

    private UIElement HistoryRow(NotesHistoryItem item)
    {
        var id = item.Id;
        var preview = HeaderLabel(item.Preview, _historyCaption.Foreground);
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        preview.MaxWidth = 196;
        preview.Cursor = Cursors.Hand;
        preview.VerticalAlignment = VerticalAlignment.Center;
        preview.ToolTip = "替换当前便笺";
        void Restore() => RestoreRequested?.Invoke(this, id);
        preview.MouseLeftButtonUp += (_, e) =>
        {
            Restore();
            e.Handled = true;
        };
        var trash = HeaderGlyph("\uE74D", _historyCaption.Foreground, () => DiscardRequested?.Invoke(this, id));
        trash.ToolTip = "永久删除";
        trash.Padding = new Thickness(8, 4, 0, 4);
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = TokenBrushes.From(NotePapers.Fill(item.Paper)),
            Stroke = TokenBrushes.From(0x33000000),
            StrokeThickness = 0.6,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0), Cursor = Cursors.Hand };
        DockPanel.SetDock(trash, Dock.Right);
        row.Children.Add(trash);
        row.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { dot, preview }
        });
        return row;
    }

    private (Border Dot, TextBlock Check) Swatch(string id)
    {
        var check = new TextBlock
        {
            Text = "\uE73E",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        var dot = new Grid
        {
            Width = 28,
            Height = 28,
            Children =
            {
                new Ellipse
                {
                    Fill = TokenBrushes.From(NotePapers.Fill(id)),
                    Stroke = TokenBrushes.From(0x33000000),
                    StrokeThickness = 0.6
                },
                check
            }
        };
        var border = new Border
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            Child = dot
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            Choose(id);
            e.Handled = true;
        };
        return (border, check);
    }

    private void RefreshSwatches()
    {
        foreach (var (id, check) in _marks)
        {
            check.Visibility = string.Equals(id, _paper, StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private static Grid HeaderRow(UIElement left, params UIElement[] right)
    {
        var row = new Grid();
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

    private static TextBlock HeaderLabel(string text, Brush ink) => new()
    {
        Tag = "ink",
        Text = text,
        FontFamily = WidgetFonts.Notes,
        FontSize = 13,
        Foreground = ink,
        VerticalAlignment = VerticalAlignment.Center
    };

    private Grid BuildDeleteFooter(Brush ink)
    {
        var trash = HeaderGlyph("\uE74D", ink, AskDelete);
        trash.HorizontalAlignment = HorizontalAlignment.Right;
        trash.VerticalAlignment = VerticalAlignment.Bottom;
        trash.ToolTip = "删除这张便笺";
        _deleteButton = trash;
        var cancel = TextAction("取消", ink, HideDeleteConfirm);
        var confirm = TextAction("确认删除", ink, RequestDelete);
        ((TextBlock)confirm.Child).Opacity = 0.82;
        confirm.ToolTip = "永久删除，不会进入历史";
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, confirm }
        };
        _deleteConfirm = new Border
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = Visibility.Collapsed,
            Child = actions
        };
        var footer = new Grid
        {
            MinHeight = 28,
            Margin = new Thickness(0, 8, 0, 0)
        };
        footer.Children.Add(_deleteButton);
        footer.Children.Add(_deleteConfirm);
        return footer;
    }

    private void AskDelete()
    {
        _popup.StaysOpen = true;
        _deleteButton.Visibility = Visibility.Collapsed;
        _deleteConfirm.Visibility = Visibility.Visible;
    }

    private void HideDeleteConfirm()
    {
        if (_deleteButton is null || _deleteConfirm is null)
        {
            return;
        }

        _deleteButton.Visibility = Visibility.Visible;
        _deleteConfirm.Visibility = Visibility.Collapsed;
        if (_customizePage.Visibility != Visibility.Visible)
        {
            _popup.StaysOpen = false;
        }
    }

    private static Border TextAction(string text, Brush ink, Action click)
    {
        var label = HeaderLabel(text, ink);
        label.Opacity = 0.55;
        var border = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Padding = new Thickness(8, 4, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = label
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return border;
    }

    private static TextBlock HeaderGlyph(string glyph, Brush ink, Action click, double size = 13)
    {
        var block = new TextBlock
        {
            Tag = "ink",
            Text = glyph,
            FontFamily = glyph == "\u00D7" ? WidgetFonts.Ui : new FontFamily("Segoe MDL2 Assets"),
            FontSize = size,
            Foreground = ink,
            Opacity = 0.55,
            Padding = new Thickness(8, 4, 0, 4),
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        block.MouseLeftButtonUp += (_, e) =>
        {
            click();
            e.Handled = true;
        };
        return block;
    }

    private void RequestDelete()
    {
        Discard();
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        if (_discarded)
        {
            return;
        }

        var text = _box.Text ?? string.Empty;
        var editorOpen = _editorPage.Visibility == Visibility.Visible;
        if (NotesText.IsBlank(text) && !NotesText.IsBlank(_committed) && !editorOpen)
        {
            _preview.Text = PreviewText(_committed);
            FitPreview(_committed);
            return;
        }

        try
        {
            _store.Save(_key, text);
            _committed = text;
            _preview.Text = PreviewText(text);
            FitPreview(text);
        }
        catch (Exception)
        {
            _preview.Text = PreviewText(text);
            FitPreview(text);
            UserAlerts.Show("WinDock 便笺", "便笺未能写入磁盘。");
        }
    }

    private void FitPreview(string text)
    {
        _preview.MaxHeight = NotesText.HasTitle(text) ? 14 : 28;
    }

    private static string PreviewText(string text) => NotesText.DockPreview(text);
}
