using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Windows.Threading;
using MDViewer.Models;
using MDViewer.Services;
using MDViewer.ViewModels;

namespace MDViewer;

public partial class MainWindow : Window
{
    private const double DocumentWheelScale = 1.85;
    private const double EditorWheelScale = 1.65;
    private static readonly TimeSpan DocumentWheelDuration = TimeSpan.FromMilliseconds(190);
    private static readonly TimeSpan EditorWheelDuration = TimeSpan.FromMilliseconds(170);

    private readonly MainViewModel _viewModel;
    private readonly DwmBackdropService _dwmBackdropService;
    private readonly DispatcherTimer _documentScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _editorScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private ScrollViewer? _documentScrollViewer;
    private double _documentScrollStart;
    private double _documentScrollTarget;
    private DateTime _documentScrollStarted;
    private double _editorScrollStart;
    private double _editorScrollTarget;
    private DateTime _editorScrollStarted;

    public MainWindow()
    {
        InitializeComponent();

        var themeService = new ThemeService();
        var fontCacheService = new FontCacheService();
        var rendererService = new MarkdownRendererService(themeService);
        var recentFileService = new RecentFileService();
        var fileWatcherService = new FileWatcherService();
        var tweenService = new TweenService();

        _dwmBackdropService = new DwmBackdropService();
        _viewModel = new MainViewModel(
            rendererService,
            themeService,
            fontCacheService,
            recentFileService,
            fileWatcherService,
            tweenService,
            _dwmBackdropService);

        DataContext = _viewModel;
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closed += (_, _) => _viewModel.Dispose();
        AddHandler(DragDrop.PreviewDragEnterEvent, new DragEventHandler(Root_DragEnter), true);
        AddHandler(DragDrop.PreviewDragOverEvent, new DragEventHandler(Root_DragOver), true);
        AddHandler(DragDrop.PreviewDragLeaveEvent, new DragEventHandler(Root_DragLeave), true);
        AddHandler(DragDrop.PreviewDropEvent, new DragEventHandler(Root_Drop), true);
        _documentScrollTimer.Tick += (_, _) => TickDocumentScroll();
        _editorScrollTimer.Tick += (_, _) => TickEditorScroll();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dwmBackdropService.Apply(this);
        _viewModel.AttachWindow(this);
        UpdateWindowShape();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateWindowShape();
    }

    private void Root_DragEnter(object sender, DragEventArgs e)
    {
        UpdateDropOverlay(e);
    }

    private void Root_DragLeave(object sender, DragEventArgs e)
    {
        if (!IsMouseInsideWindow())
        {
            HideDropOverlay();
        }
        e.Handled = true;
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropOverlay(e);
        e.Handled = true;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            await _viewModel.OpenDroppedFileAsync(files);
        }

        HideDropOverlay();
        e.Handled = true;
    }

    private void DocumentViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (GetDocumentScrollViewer() is not { } scrollViewer)
        {
            return;
        }

        var currentBase = _documentScrollTimer.IsEnabled ? _documentScrollTarget : scrollViewer.VerticalOffset;
        SmoothScrollDocumentTo(currentBase - e.Delta * DocumentWheelScale, DocumentWheelDuration);
        e.Handled = true;
    }

    private void EditorTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var currentBase = _editorScrollTimer.IsEnabled ? _editorScrollTarget : EditorTextBox.VerticalOffset;
        SmoothScrollEditorTo(currentBase - e.Delta * EditorWheelScale, EditorWheelDuration);
        e.Handled = true;
    }

    private void OutlineListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutlineListBox.SelectedItem is TableOfContentsItem item)
        {
            ScrollToTableOfContentsItem(item);
        }
    }

    private void InsertMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string token })
        {
            InsertMarkdownToken(token);
        }
    }

    private void UpdateDropOverlay(DragEventArgs e)
    {
        var hasMarkdown = e.Data.GetData(DataFormats.FileDrop) is string[] files
            && Array.Exists(files, file =>
                string.Equals(System.IO.Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase)
                || string.Equals(System.IO.Path.GetExtension(file), ".markdown", StringComparison.OrdinalIgnoreCase));

        e.Effects = hasMarkdown ? DragDropEffects.Copy : DragDropEffects.None;
        if (hasMarkdown)
        {
            ShowDropOverlay();
        }
        else
        {
            HideDropOverlay();
        }
        e.Handled = true;
    }

    private void ShowDropOverlay()
    {
        DropOverlay.Visibility = Visibility.Visible;
        TitleBarSurface.Effect = new BlurEffect { Radius = 12, KernelType = KernelType.Gaussian };
        BodySurface.Effect = new BlurEffect { Radius = 12, KernelType = KernelType.Gaussian };
    }

    private void HideDropOverlay()
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        TitleBarSurface.Effect = null;
        BodySurface.Effect = null;
    }

    private bool IsMouseInsideWindow()
    {
        var position = Mouse.GetPosition(this);
        return position.X >= 0 && position.Y >= 0 && position.X <= ActualWidth && position.Y <= ActualHeight;
    }

    private void UpdateWindowShape()
    {
        var rounded = WindowState != WindowState.Maximized;
        WindowFrame.CornerRadius = rounded ? new CornerRadius(18) : new CornerRadius(0);
        _dwmBackdropService.ApplyRoundedCorners(this, rounded);
    }

    private ScrollViewer? GetDocumentScrollViewer()
    {
        _documentScrollViewer ??= FindVisualChild<ScrollViewer>(DocumentViewer);
        return _documentScrollViewer;
    }

    private void SmoothScrollDocumentTo(double offset, TimeSpan duration)
    {
        var scrollViewer = GetDocumentScrollViewer();
        if (scrollViewer is null)
        {
            return;
        }

        _documentScrollStart = scrollViewer.VerticalOffset;
        _documentScrollTarget = Math.Clamp(offset, 0, scrollViewer.ScrollableHeight);
        _documentScrollStarted = DateTime.UtcNow;
        _documentScrollTimer.Tag = duration;
        _documentScrollTimer.Start();
    }

    private void SmoothScrollEditorTo(double offset, TimeSpan duration)
    {
        _editorScrollStart = EditorTextBox.VerticalOffset;
        _editorScrollTarget = Math.Clamp(offset, 0, EditorTextBox.ExtentHeight);
        _editorScrollStarted = DateTime.UtcNow;
        _editorScrollTimer.Tag = duration;
        _editorScrollTimer.Start();
    }

    private void TickDocumentScroll()
    {
        var scrollViewer = GetDocumentScrollViewer();
        if (scrollViewer is null)
        {
            _documentScrollTimer.Stop();
            return;
        }

        var duration = (TimeSpan)(_documentScrollTimer.Tag ?? TimeSpan.FromMilliseconds(260));
        var progress = Math.Clamp((DateTime.UtcNow - _documentScrollStarted).TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        scrollViewer.ScrollToVerticalOffset(_documentScrollStart + ((_documentScrollTarget - _documentScrollStart) * eased));

        if (progress >= 1)
        {
            _documentScrollTimer.Stop();
        }
    }

    private void TickEditorScroll()
    {
        var duration = (TimeSpan)(_editorScrollTimer.Tag ?? TimeSpan.FromMilliseconds(220));
        var progress = Math.Clamp((DateTime.UtcNow - _editorScrollStarted).TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        EditorTextBox.ScrollToVerticalOffset(_editorScrollStart + ((_editorScrollTarget - _editorScrollStart) * eased));

        if (progress >= 1)
        {
            _editorScrollTimer.Stop();
        }
    }

    private void ScrollToTableOfContentsItem(TableOfContentsItem item)
    {
        if (item.TargetBlock is null || GetDocumentScrollViewer() is not { } scrollViewer)
        {
            return;
        }

        DocumentViewer.UpdateLayout();
        var rect = item.TargetBlock.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        var target = scrollViewer.VerticalOffset + rect.Top - 26;
        SmoothScrollDocumentTo(target, TimeSpan.FromMilliseconds(420));
    }

    private void InsertMarkdownToken(string token)
    {
        var textBox = EditorTextBox;
        var selected = textBox.SelectedText;
        var insert = token switch
        {
            "h1" => PrefixLine(textBox, "# "),
            "h2" => PrefixLine(textBox, "## "),
            "bold" => WrapSelection(textBox, "**", "**", "굵은 텍스트"),
            "italic" => WrapSelection(textBox, "*", "*", "기울임 텍스트"),
            "code" => WrapSelection(textBox, "`", "`", "code"),
            "codeblock" => string.IsNullOrEmpty(selected) ? "```csharp\r\n\r\n```" : $"```\r\n{selected}\r\n```",
            "quote" => PrefixLine(textBox, "> "),
            "ul" => PrefixLine(textBox, "- "),
            "ol" => PrefixLine(textBox, "1. "),
            "task" => PrefixLine(textBox, "- [ ] "),
            "link" => string.IsNullOrEmpty(selected) ? "[링크 텍스트](https://)" : $"[{selected}](https://)",
            "image" => "![이미지 설명](path/to/image.png)",
            "table" => "| 항목 | 설명 |\r\n| --- | --- |\r\n| 값 | 내용 |",
            "hr" => "\r\n---\r\n",
            _ => string.Empty
        };

        if (token is "h1" or "h2" or "quote" or "ul" or "ol" or "task")
        {
            return;
        }

        ReplaceSelection(textBox, insert);
    }

    private static string WrapSelection(TextBox textBox, string prefix, string suffix, string placeholder)
    {
        var selected = string.IsNullOrEmpty(textBox.SelectedText) ? placeholder : textBox.SelectedText;
        return $"{prefix}{selected}{suffix}";
    }

    private static string PrefixLine(TextBox textBox, string prefix)
    {
        var lineIndex = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex);
        var lineStart = textBox.GetCharacterIndexFromLineIndex(lineIndex);
        textBox.Text = textBox.Text.Insert(lineStart, prefix);
        textBox.CaretIndex = Math.Min(textBox.Text.Length, lineStart + prefix.Length);
        textBox.Focus();
        return string.Empty;
    }

    private static void ReplaceSelection(TextBox textBox, string text)
    {
        var start = textBox.SelectionStart;
        textBox.SelectedText = text;
        textBox.SelectionStart = start;
        textBox.SelectionLength = text.Length;
        textBox.Focus();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed && e.GetPosition(this).Y <= 44)
        {
            DragMove();
        }
    }
}
