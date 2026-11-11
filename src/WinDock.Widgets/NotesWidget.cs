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

public sealed class NotesWidget : UserControl, IWidgetOpenable, IWidgetCustomizable, INotesHistory, ILocalizable, IDockEdgeAware
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
    private IReadOnlyList<NotesHistoryItem> _history = [];
    private TextBlock _flyoutTitle = null!;
    private PhosphorGlyph _todoGlyph = null!;
    private TextBlock _customizeTitle = null!;
    private TextBlock _backLabel = null!;
    private TextBlock _paperCaption = null!;
    private TextBlock _syntaxCaption = null!;
    private TextBlock _syntaxHint = null!;
    private PhosphorGlyph _customizeGlyph = null!;
    private TextBlock _deleteCancel = null!;
    private TextBlock _deleteConfirmLabel = null!;
    private readonly ScrollViewer _historyScroll = new()
    {
        MaxHeight = 148,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Margin = new Thickness(0, 8, 0, 0)
    };
    private readonly List<(string Id, PhosphorGlyph Check)> _marks = [];
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly Popup _popup;
    private bool _side;
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
            MinHeight = 148,
            MaxHeight = 280,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
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
            else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleCurrentLineTodo();
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

    public void ApplyLocale()
    {
        _flyoutTitle.Text = UiText.WidgetNotes;
        if (_todoGlyph is not null)
        {
            _todoGlyph.ToolTip = UiText.NotesTodoToggle;
        }
        _customizeGlyph.ToolTip = UiText.Customize;
        _backLabel.Text = UiText.Back;
        _customizeTitle.Text = UiText.Customize;
        _paperCaption.Text = UiText.PaperColor;
        _syntaxCaption.Text = UiText.NotesSyntax;
        _syntaxHint.Text = UiText.NotesSyntaxHint;
        _historyCaption.Text = UiText.NotesHistory;
        _deleteButton.ToolTip = UiText.DeleteThisNote;
        _deleteCancel.Text = UiText.Cancel;
        _deleteConfirmLabel.Text = UiText.ConfirmDelete;
        _deleteConfirm.ToolTip = UiText.DeleteNoteForeverHint;
        var text = _box.Text ?? string.Empty;
        _preview.Text = PreviewText(text);
        FitPreview(text);
        ApplyHistory(_history);
    }

    public void ApplyHistory(IReadOnlyList<NotesHistoryItem> items)
    {
        _history = items;
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

    public void ApplyDockEdge(DockEdge edge)
    {
        _side = WidgetChrome.IsSide(edge);
        FitPreview(_preview.Text);
        WidgetChrome.FitEdge(_tile, _side, 0);
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
        _flyoutTitle = HeaderLabel(UiText.WidgetNotes, ink);
        _todoGlyph = WidgetChrome.HeaderIcon(PhosphorIcon.CheckSquare, ToggleCurrentLineTodo, foreground: ink, tag: "ink", opacity: 0.55);
        _todoGlyph.ToolTip = UiText.NotesTodoToggle;
        _customizeGlyph = WidgetChrome.HeaderIcon(PhosphorIcon.SlidersHorizontal, OpenCustomize, foreground: ink, tag: "ink", opacity: 0.55);
        _customizeGlyph.ToolTip = UiText.Customize;
        var close = WidgetChrome.HeaderClose(ClosePopup, 18, ink, "ink", 0.55);
        var header = HeaderRow(_flyoutTitle, _todoGlyph, _customizeGlyph, close);
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

    private void ToggleCurrentLineTodo()
    {
        var text = _box.Text ?? string.Empty;
        var caret = _box.CaretIndex;
        var lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var hasCarriageReturn = lineEnd > lineStart && text[lineEnd - 1] == '\r';
        var actualEnd = hasCarriageReturn ? lineEnd - 1 : lineEnd;
        var line = text.Substring(lineStart, actualEnd - lineStart);

        string newLine;
        if (NotesText.IsTodoLine(line))
        {
            newLine = NotesText.ToggleTodoLine(line);
        }
        else
        {
            newLine = "- [ ] " + line;
        }

        var newText = text.Substring(0, lineStart) + newLine + (hasCarriageReturn ? "\r" : "") + text.Substring(lineEnd);
        _box.Text = newText;
        var diff = newLine.Length - line.Length;
        _box.CaretIndex = Math.Clamp(caret + diff, 0, newText.Length);
        _box.Focus();
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
                WidgetChrome.HeaderIcon(PhosphorIcon.CaretLeft, ShowEditor, 12, ink, "ink", 0.55),
                HeaderLabel(UiText.Back, ink)
            }
        };
        _backLabel = (TextBlock)back.Children[1];
        back.MouseLeftButtonUp += (_, e) =>
        {
            ShowEditor();
            e.Handled = true;
        };
        _customizeTitle = HeaderLabel(UiText.Customize, ink);
        _customizeTitle.Margin = new Thickness(8, 0, 0, 0);
        _customizeTitle.FontWeight = FontWeights.SemiBold;
        var close = WidgetChrome.HeaderClose(ClosePopup, 18, ink, "ink", 0.55);
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { back, _customizeTitle }
        };
        var header = HeaderRow(left, close);
        _paperCaption = HeaderLabel(UiText.PaperColor, ink);
        _paperCaption.Opacity = 0.62;
        _paperCaption.Margin = new Thickness(0, 14, 0, 0);
        foreach (var id in NotePapers.All)
        {
            var (dot, check) = Swatch(id);
            _swatches.Children.Add(dot);
            _marks.Add((id, check));
        }

        _historyCaption.Tag = "ink";
        _historyCaption.Text = UiText.NotesHistory;
        _historyCaption.FontFamily = WidgetFonts.Notes;
        _historyCaption.FontSize = 13;
        _historyCaption.Foreground = ink;
        _historyCaption.Opacity = 0.62;
        _historyCaption.Margin = new Thickness(0, 16, 0, 0);
        _historyCaption.Visibility = Visibility.Collapsed;
        _historyScroll.Content = _historyList;
        _historyScroll.Visibility = Visibility.Collapsed;

        _syntaxCaption = HeaderLabel(UiText.NotesSyntax, ink);
        _syntaxCaption.Opacity = 0.62;
        _syntaxCaption.Margin = new Thickness(0, 16, 0, 0);
        _syntaxHint = HeaderLabel(UiText.NotesSyntaxHint, ink);
        _syntaxHint.TextWrapping = TextWrapping.Wrap;
        _syntaxHint.Opacity = 0.78;
        _syntaxHint.Margin = new Thickness(0, 6, 0, 0);
        _syntaxHint.MaxWidth = 268;

        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.Children.Add(header);
        Grid.SetRow(_paperCaption, 1);
        page.Children.Add(_paperCaption);
        Grid.SetRow(_swatches, 2);
        page.Children.Add(_swatches);
        Grid.SetRow(_syntaxCaption, 3);
        page.Children.Add(_syntaxCaption);
        Grid.SetRow(_syntaxHint, 4);
        page.Children.Add(_syntaxHint);
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
        preview.ToolTip = UiText.ReplaceCurrentNote;
        void Restore() => RestoreRequested?.Invoke(this, id);
        preview.MouseLeftButtonUp += (_, e) =>
        {
            Restore();
            e.Handled = true;
        };
        var trash = WidgetChrome.HeaderIcon(PhosphorIcon.Trash, () => DiscardRequested?.Invoke(this, id), foreground: _historyCaption.Foreground, tag: "ink", opacity: 0.55);
        trash.ToolTip = UiText.DeleteForever;
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

    private (Border Dot, PhosphorGlyph Check) Swatch(string id)
    {
        var check = new PhosphorGlyph(PhosphorIcon.Check, 11)
        {
            Tag = "tint",
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
            Width = DockTokens.FlyoutControlHeight,
            Height = DockTokens.FlyoutControlHeight,
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
        var trash = WidgetChrome.HeaderIcon(PhosphorIcon.Trash, AskDelete, foreground: ink, tag: "ink", opacity: 0.55);
        trash.HorizontalAlignment = HorizontalAlignment.Right;
        trash.VerticalAlignment = VerticalAlignment.Bottom;
        trash.ToolTip = UiText.DeleteThisNote;
        _deleteButton = trash;
        var cancel = TextAction(UiText.Cancel, ink, HideDeleteConfirm, out _deleteCancel);
        var confirm = TextAction(UiText.ConfirmDelete, ink, RequestDelete, out _deleteConfirmLabel);
        _deleteConfirmLabel.Opacity = 0.82;
        confirm.ToolTip = UiText.DeleteNoteForeverHint;
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
            MinHeight = DockTokens.FlyoutControlHeight,
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

    private static Border TextAction(string text, Brush ink, Action click, out TextBlock label)
    {
        label = HeaderLabel(text, ink);
        label.Opacity = 0.55;
        var border = new Border
        {
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Height = DockTokens.FlyoutControlHeight,
            Padding = new Thickness(10, 0, 10, 0),
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
            UserAlerts.Show(UiText.NotesAlertTitle, UiText.NotesSaveFailed);
        }
    }

    private void FitPreview(string text)
    {
        if (_side)
        {
            _preview.Width = DockTokens.SideTileWidth - 12;
            _preview.MaxHeight = 42;
            _preview.TextAlignment = TextAlignment.Left;
            return;
        }

        _preview.Width = 132;
        _preview.TextAlignment = TextAlignment.Left;
        _preview.MaxHeight = NotesText.HasTitle(text) ? 14 : 28;
    }

    private static string PreviewText(string text) => NotesText.DockPreview(text);
}
