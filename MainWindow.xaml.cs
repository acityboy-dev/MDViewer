using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using MDViewer.Models;
using MDViewer.Services;
using MDViewer.ViewModels;

namespace MDViewer;

public partial class MainWindow : Window
{
    private const double DocumentWheelScale = 1.45;
    private const double EditorWheelScale = 1.35;

    private readonly MainViewModel _viewModel;
    private readonly DwmBackdropService _dwmBackdropService;
    private ScrollViewer? _documentScrollViewer;

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
            _dwmBackdropService,
            App.LanguageService);

        DataContext = _viewModel;
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closed += (_, _) => _viewModel.Dispose();
        AddHandler(DragDrop.PreviewDragEnterEvent, new DragEventHandler(Root_DragEnter), true);
        AddHandler(DragDrop.PreviewDragOverEvent, new DragEventHandler(Root_DragOver), true);
        AddHandler(DragDrop.PreviewDragLeaveEvent, new DragEventHandler(Root_DragLeave), true);
        AddHandler(DragDrop.PreviewDropEvent, new DragEventHandler(Root_Drop), true);
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

    private void OutlineListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutlineListBox.SelectedItem is TableOfContentsItem item)
        {
            ScrollToTableOfContentsItem(item);
        }
    }

    private void DocumentViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (GetDocumentScrollViewer() is not { } scrollViewer)
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(
            Math.Clamp(scrollViewer.VerticalOffset - (e.Delta * DocumentWheelScale), 0, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    private void EditorTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        EditorTextBox.ScrollToVerticalOffset(
            Math.Clamp(EditorTextBox.VerticalOffset - (e.Delta * EditorWheelScale), 0, EditorTextBox.ExtentHeight));
        e.Handled = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = this,
            DataContext = _viewModel
        };
        settingsWindow.ShowDialog();
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        var fadeOut = new DoubleAnimation(1, 0.42, TimeSpan.FromMilliseconds(95))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        fadeOut.Completed += (_, _) =>
        {
            _viewModel.ToggleThemeCommand.Execute(null);

            var fadeIn = new DoubleAnimation(0.42, 1, TimeSpan.FromMilliseconds(145))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            WindowFrame.BeginAnimation(OpacityProperty, fadeIn);
        };

        WindowFrame.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void CurrentFilePath_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!File.Exists(_viewModel.CurrentFilePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_viewModel.CurrentFilePath}\"")
        {
            UseShellExecute = true
        });
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
        TitleBarSurface.Padding = rounded ? new Thickness(0) : new Thickness(8, 0, 8, 0);
        _dwmBackdropService.ApplyRoundedCorners(this, rounded);
    }

    private void RoundedContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || element.Tag is not string radiusText
            || !double.TryParse(radiusText, out var radius)
            || e.NewSize.Width <= 0
            || e.NewSize.Height <= 0)
        {
            return;
        }

        element.Clip = new RectangleGeometry(new Rect(e.NewSize), radius, radius);
    }

    private ScrollViewer? GetDocumentScrollViewer()
    {
        _documentScrollViewer ??= FindVisualChild<ScrollViewer>(DocumentViewer);
        return _documentScrollViewer;
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
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(target, 0, scrollViewer.ScrollableHeight));
    }

    private void InsertMarkdownToken(string token)
    {
        var textBox = EditorTextBox;
        var selected = textBox.SelectedText;
        var insert = token switch
        {
            "h1" => PrefixLine(textBox, "# "),
            "h2" => PrefixLine(textBox, "## "),
            "bold" => WrapSelection(textBox, "**", "**", _viewModel.Localize("PlaceholderBold")),
            "italic" => WrapSelection(textBox, "*", "*", _viewModel.Localize("PlaceholderItalic")),
            "code" => WrapSelection(textBox, "`", "`", "code"),
            "codeblock" => string.IsNullOrEmpty(selected) ? "```csharp\r\n\r\n```" : $"```\r\n{selected}\r\n```",
            "quote" => PrefixLine(textBox, "> "),
            "ul" => PrefixLine(textBox, "- "),
            "ol" => PrefixLine(textBox, "1. "),
            "task" => PrefixLine(textBox, "- [ ] "),
            "link" => string.IsNullOrEmpty(selected) ? $"[{_viewModel.Localize("PlaceholderLink")}](https://)" : $"[{selected}](https://)",
            "image" => $"![{_viewModel.Localize("PlaceholderImage")}](path/to/image.png)",
            "table" => $"| {_viewModel.Localize("PlaceholderTableItem")} | {_viewModel.Localize("PlaceholderTableDescription")} |\r\n| --- | --- |\r\n| {_viewModel.Localize("PlaceholderTableValue")} | {_viewModel.Localize("PlaceholderTableContent")} |",
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
        if (e.ClickCount == 2 && e.GetPosition(this).Y <= 44)
        {
            Maximize_Click(this, new RoutedEventArgs());
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed && e.GetPosition(this).Y <= 44)
        {
            DragMove();
        }
    }
}
