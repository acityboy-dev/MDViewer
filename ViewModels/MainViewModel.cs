using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.Win32;
using MDViewer.Models;
using MDViewer.Services;
using MDViewer.Utilities;
using ThemeMode = MDViewer.Models.ThemeMode;

namespace MDViewer.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly MarkdownRendererService _rendererService;
    private readonly ThemeService _themeService;
    private readonly FontCacheService _fontCacheService;
    private readonly RecentFileService _recentFileService;
    private readonly FileWatcherService _fileWatcherService;
    private readonly TweenService _tweenService;
    private readonly DwmBackdropService _dwmBackdropService;
    private readonly LanguageService _languageService;
    private readonly AppSettingsService _appSettingsService;
    private readonly DispatcherTimer _editorPreviewTimer;
    private Window? _window;
    private string _currentFilePath = string.Empty;
    private string _documentTitle = string.Empty;
    private string _documentSubtitle = string.Empty;
    private string _documentInfo = string.Empty;
    private string _renderStatus = string.Empty;
    private string _markdownText = string.Empty;
    private FlowDocument? _document;
    private FlowDocument? _editorPreviewDocument;
    private GridLength _sidebarWidth = new(286);
    private GridLength _sidebarGapWidth = new(14);
    private GridLength _editorPreviewWidth = new(0);
    private GridLength _editorPreviewGapWidth = new(0);
    private double _sidebarOpacity = 1;
    private bool _isSidebarOpen = true;
    private bool _sidebarWasOpenBeforeCompact = true;
    private bool _isCompactMode;
    private bool _isEditing;
    private bool _isDirty;
    private bool _isLoadingDocument;
    private bool _isFileWatchingEnabled = true;
    private int _zoomPercent = 100;
    private bool _isAlwaysOnTop;
    private ThemeMode _selectedThemeMode = ThemeMode.Light;
    private TypographyPreset _selectedTypographyPreset = TypographyPreset.Comfortable;
    private EditorMode _selectedEditorMode = EditorMode.Markdown;
    private AppLanguage _selectedLanguage;
    private DocumentFontOption? _selectedDocumentFontOption;
    private RecentFileEntry? _selectedRecentFile;
    private bool _isSelectingRecentFile;

    public MainViewModel(
        MarkdownRendererService rendererService,
        ThemeService themeService,
        FontCacheService fontCacheService,
        RecentFileService recentFileService,
        FileWatcherService fileWatcherService,
        TweenService tweenService,
        DwmBackdropService dwmBackdropService,
        LanguageService languageService,
        AppSettingsService appSettingsService)
    {
        _rendererService = rendererService;
        _themeService = themeService;
        _fontCacheService = fontCacheService;
        _recentFileService = recentFileService;
        _fileWatcherService = fileWatcherService;
        _tweenService = tweenService;
        _dwmBackdropService = dwmBackdropService;
        _languageService = languageService;
        _appSettingsService = appSettingsService;

        var settings = appSettingsService.Current;
        _selectedLanguage = languageService.CurrentLanguage;
        _selectedThemeMode = settings.Theme;
        _selectedTypographyPreset = settings.Typography;
        _selectedEditorMode = settings.EditorMode;
        _isFileWatchingEnabled = settings.IsFileWatchingEnabled;
        _zoomPercent = NormalizeZoom(settings.ZoomPercent);
        _isAlwaysOnTop = settings.IsAlwaysOnTop;
        _selectedDocumentFontOption = DocumentFontOptions.FirstOrDefault(option => option.Id == settings.DocumentFontId)
            ?? DocumentFontOptions.First(option => option.Id == "system");

        _themeService.ApplyTheme(_selectedThemeMode);
        _themeService.ApplyTypography(_selectedTypographyPreset);
        _themeService.ApplyZoom(_zoomPercent);

        OpenFileCommand = new AsyncRelayCommand(OpenFileWithDialogAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => HasDocument);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasDocument && IsDirty);
        ToggleEditCommand = new RelayCommand(ToggleEdit, () => HasDocument);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        ToggleCompactCommand = new RelayCommand(ToggleCompactMode);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ZoomInCommand = new RelayCommand(ZoomIn, () => ZoomPercent < 200);
        ZoomOutCommand = new RelayCommand(ZoomOut, () => ZoomPercent > 50);
        ResetZoomCommand = new RelayCommand(ResetZoom, () => ZoomPercent != 100);
        ToggleAlwaysOnTopCommand = new RelayCommand(ToggleAlwaysOnTop);

        _editorPreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _editorPreviewTimer.Tick += (_, _) =>
        {
            _editorPreviewTimer.Stop();
            RenderEditorPreview();
        };

        RefreshLocalizedOptions();
        RefreshLocalizedContent();

        foreach (var item in _recentFileService.Load())
        {
            RecentFiles.Add(item);
        }

        _fileWatcherService.FileChanged += async (_, path) =>
        {
            if (string.Equals(path, CurrentFilePath, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                await Application.Current.Dispatcher.InvokeAsync(async () => await LoadFileAsync(path, addToRecent: false));
            }
        };
    }

    public string Title => HasDocument ? $"{DocumentTitle} - MarkFlow" : "MarkFlow";
    public ObservableCollection<RecentFileEntry> RecentFiles { get; } = [];
    public ObservableCollection<TableOfContentsItem> TableOfContents { get; } = [];
    public ObservableCollection<SelectionOption<ThemeMode>> ThemeModeOptions { get; } = [];
    public ObservableCollection<SelectionOption<TypographyPreset>> TypographyPresetOptions { get; } = [];
    public ObservableCollection<SelectionOption<EditorMode>> EditorModeOptions { get; } = [];
    public ObservableCollection<SelectionOption<AppLanguage>> LanguageOptions { get; } = [];

    public IReadOnlyList<DocumentFontOption> DocumentFontOptions { get; } =
    [
        new(
            "notosanskr",
            "Noto Sans KR",
            "Noto Sans KR",
            "Malgun Gothic, Segoe UI",
            "https://github.com/google/fonts/raw/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf",
            "https://raw.githubusercontent.com/google/fonts/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf"),
        new(
            "ibmplexsanskr",
            "IBM Plex Sans KR",
            "IBM Plex Sans KR",
            "Malgun Gothic, Segoe UI",
            "https://github.com/google/fonts/raw/main/ofl/ibmplexsanskr/IBMPlexSansKR-Regular.ttf",
            "https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexsanskr/IBMPlexSansKR-Regular.ttf"),
        new(
            "nanumgothic",
            "나눔고딕",
            "NanumGothic",
            "Malgun Gothic, Segoe UI",
            "https://github.com/google/fonts/raw/main/ofl/nanumgothic/NanumGothic-Regular.ttf",
            "https://raw.githubusercontent.com/google/fonts/main/ofl/nanumgothic/NanumGothic-Regular.ttf"),
        new(
            "gowundodum",
            "고운돋움",
            "Gowun Dodum",
            "Malgun Gothic, Segoe UI",
            "https://github.com/google/fonts/raw/main/ofl/gowundodum/GowunDodum-Regular.ttf",
            "https://raw.githubusercontent.com/google/fonts/main/ofl/gowundodum/GowunDodum-Regular.ttf"),
        new(
            "notoserifkr",
            "Noto Serif KR",
            "Noto Serif KR",
            "Batang, Malgun Gothic",
            "https://github.com/google/fonts/raw/main/ofl/notoserifkr/NotoSerifKR%5Bwght%5D.ttf",
            "https://raw.githubusercontent.com/google/fonts/main/ofl/notoserifkr/NotoSerifKR%5Bwght%5D.ttf"),
        new("system", "맑은 고딕", "Malgun Gothic", "Malgun Gothic, Segoe UI")
    ];

    public AsyncRelayCommand OpenFileCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand ToggleEditCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ToggleCompactCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public RelayCommand ToggleAlwaysOnTopCommand { get; }

    public string CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (SetProperty(ref _currentFilePath, value))
            {
                OnPropertyChanged(nameof(HasDocument));
                OnPropertyChanged(nameof(Title));
                ReloadCommand.RaiseCanExecuteChanged();
                ToggleEditCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DocumentTitle
    {
        get => _documentTitle;
        private set
        {
            if (SetProperty(ref _documentTitle, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string DocumentSubtitle
    {
        get => _documentSubtitle;
        private set => SetProperty(ref _documentSubtitle, value);
    }

    public string DocumentInfo
    {
        get => _documentInfo;
        private set => SetProperty(ref _documentInfo, value);
    }

    public string RenderStatus
    {
        get => _renderStatus;
        private set => SetProperty(ref _renderStatus, value);
    }

    public FlowDocument? Document
    {
        get => _document;
        private set => SetProperty(ref _document, value);
    }

    public FlowDocument? EditorPreviewDocument
    {
        get => _editorPreviewDocument;
        private set => SetProperty(ref _editorPreviewDocument, value);
    }

    public bool HasDocument => !string.IsNullOrWhiteSpace(CurrentFilePath);

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            if (SetProperty(ref _markdownText, value) && HasDocument && !_isLoadingDocument)
            {
                IsDirty = true;
                ScheduleEditorPreviewRender();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(ModeLabel));
            }
        }
    }

    public string ModeLabel => IsEditing ? Localize("ModeView") : Localize("ModeEdit");

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(DirtyLabel));
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DirtyLabel => IsDirty ? Localize("StateModified") : Localize("StateSaved");

    public GridLength SidebarWidth
    {
        get => _sidebarWidth;
        private set => SetProperty(ref _sidebarWidth, value);
    }

    public GridLength SidebarGapWidth
    {
        get => _sidebarGapWidth;
        private set => SetProperty(ref _sidebarGapWidth, value);
    }

    public GridLength EditorPreviewWidth
    {
        get => _editorPreviewWidth;
        private set => SetProperty(ref _editorPreviewWidth, value);
    }

    public GridLength EditorPreviewGapWidth
    {
        get => _editorPreviewGapWidth;
        private set => SetProperty(ref _editorPreviewGapWidth, value);
    }

    public double SidebarOpacity
    {
        get => _sidebarOpacity;
        private set => SetProperty(ref _sidebarOpacity, value);
    }

    public bool IsCompactMode
    {
        get => _isCompactMode;
        private set => SetProperty(ref _isCompactMode, value);
    }

    public bool IsFileWatchingEnabled
    {
        get => _isFileWatchingEnabled;
        set
        {
            if (SetProperty(ref _isFileWatchingEnabled, value))
            {
                _appSettingsService.Update(settings => settings.IsFileWatchingEnabled = value);
                UpdateWatcher();
            }
        }
    }

    public int ZoomPercent
    {
        get => _zoomPercent;
        private set
        {
            if (SetProperty(ref _zoomPercent, value))
            {
                OnPropertyChanged(nameof(ZoomLabel));
                ZoomInCommand.RaiseCanExecuteChanged();
                ZoomOutCommand.RaiseCanExecuteChanged();
                ResetZoomCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ZoomLabel => $"{ZoomPercent}%";

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        private set => SetProperty(ref _isAlwaysOnTop, value);
    }

    public ThemeMode SelectedThemeMode
    {
        get => _selectedThemeMode;
        set
        {
            if (SetProperty(ref _selectedThemeMode, value))
            {
                _appSettingsService.Update(settings => settings.Theme = value);
                _themeService.ApplyTheme(value);
                if (_window is not null)
                {
                    _dwmBackdropService.Apply(_window, _themeService.IsDarkTheme);
                    _dwmBackdropService.ApplyDarkCaption(_window, _themeService.IsDarkTheme);
                }
                OnPropertyChanged(nameof(IsDarkTheme));
                _ = ReloadAsync();
            }
        }
    }

    public TypographyPreset SelectedTypographyPreset
    {
        get => _selectedTypographyPreset;
        set
        {
            if (SetProperty(ref _selectedTypographyPreset, value))
            {
                _appSettingsService.Update(settings => settings.Typography = value);
                _themeService.ApplyTypography(value);
                _ = ReloadAsync();
            }
        }
    }

    public DocumentFontOption? SelectedDocumentFontOption
    {
        get => _selectedDocumentFontOption;
        set
        {
            if (SetProperty(ref _selectedDocumentFontOption, value) && value is not null)
            {
                _appSettingsService.Update(settings => settings.DocumentFontId = value.Id);
                _ = ApplyDocumentFontAsync(value);
            }
        }
    }

    public EditorMode SelectedEditorMode
    {
        get => _selectedEditorMode;
        set
        {
            if (SetProperty(ref _selectedEditorMode, value))
            {
                _appSettingsService.Update(settings => settings.EditorMode = value);
                UpdateEditorPreviewLayout();
                ScheduleEditorPreviewRender();
            }
        }
    }

    public AppLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value))
            {
                return;
            }

            _languageService.ApplyLanguage(value);
            RefreshLocalizedOptions();
            RefreshLocalizedContent();
            _ = ReloadAsync();
        }
    }

    public string Localize(string key) => _languageService.Get(key);
    public bool IsDarkTheme => _themeService.IsDarkTheme;

    public RecentFileEntry? SelectedRecentFile
    {
        get => _selectedRecentFile;
        set
        {
            if (SetProperty(ref _selectedRecentFile, value) && value is not null && !_isSelectingRecentFile)
            {
                _ = LoadFileAsync(value.FullPath);
            }
        }
    }

    public void AttachWindow(Window window)
    {
        _window = window;
        _dwmBackdropService.ApplyDarkCaption(window, _themeService.IsDarkTheme);
        if (_selectedDocumentFontOption is not null)
        {
            _ = ApplyDocumentFontAsync(_selectedDocumentFontOption);
        }
    }

    public async Task OpenDroppedFileAsync(IEnumerable<string> files)
    {
        var path = files.FirstOrDefault(file =>
            string.Equals(Path.GetExtension(file), ".md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(file), ".markdown", StringComparison.OrdinalIgnoreCase));
        if (path is not null)
        {
            await LoadFileAsync(path);
        }
    }

    public void Dispose()
    {
        _editorPreviewTimer.Stop();
        _fileWatcherService.Dispose();
    }

    private async Task OpenFileWithDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = Localize("FileDialogTitle"),
            Filter = Localize("FileDialogFilter"),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadFileAsync(dialog.FileName);
        }
    }

    private async Task LoadFileAsync(string path, bool addToRecent = true)
    {
        if (!File.Exists(path))
        {
            RenderStatus = Localize("FileNotFound");
            return;
        }

        try
        {
            _isLoadingDocument = true;
            RenderStatus = Localize("Rendering");
            var started = DateTime.UtcNow;
            var markdown = await File.ReadAllTextAsync(path);
            var rendered = await _rendererService.RenderFileAsync(path);

            MarkdownText = markdown;
            IsDirty = false;
            Document = rendered.Document;
            CurrentFilePath = path;
            DocumentTitle = Path.GetFileNameWithoutExtension(path);
            DocumentSubtitle = path;

            TableOfContents.Clear();
            foreach (var item in rendered.TableOfContents)
            {
                TableOfContents.Add(item);
            }

            var fileInfo = new FileInfo(path);
            DocumentInfo = BuildDocumentInfo(fileInfo, rendered);

            if (addToRecent)
            {
                _recentFileService.Add(path);
                RefreshRecentFiles(path);
            }

            UpdateWatcher();
            RenderStatus = $"{(DateTime.UtcNow - started).TotalMilliseconds:N0} ms";
        }
        catch (Exception ex)
        {
            RenderStatus = Localize("RenderFailed");
            Document = new FlowDocument(new Paragraph(new Run(ex.Message)));
        }
        finally
        {
            _isLoadingDocument = false;
            RenderEditorPreview();
        }
    }

    private async Task SaveAsync()
    {
        if (!HasDocument)
        {
            return;
        }

        _fileWatcherService.Stop();
        await File.WriteAllTextAsync(CurrentFilePath, MarkdownText);
        IsDirty = false;
        RenderStatus = Localize("SavedStatus");
        await LoadFileAsync(CurrentFilePath, addToRecent: false);
    }

    private async Task ReloadAsync()
    {
        if (HasDocument)
        {
            if (IsDirty)
            {
                RenderCurrentMarkdownPreview();
                RenderEditorPreview();
                return;
            }

            await LoadFileAsync(CurrentFilePath, addToRecent: false);
        }
    }

    private async Task ApplyDocumentFontAsync(DocumentFontOption option)
    {
        RenderStatus = option.DownloadUrls.Count == 0 ? string.Empty : Localize("PreparingFont");
        var fontFamily = await _fontCacheService.EnsureFontAsync(option);
        _themeService.ApplyDocumentFont(fontFamily);
        await ReloadAsync();
    }

    private void RefreshRecentFiles(string selectedPath)
    {
        _isSelectingRecentFile = true;
        RecentFiles.Clear();
        foreach (var item in _recentFileService.Load())
        {
            RecentFiles.Add(item);
        }

        SelectedRecentFile = RecentFiles.FirstOrDefault(item => string.Equals(item.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));
        _isSelectingRecentFile = false;
    }

    private void UpdateWatcher()
    {
        if (IsFileWatchingEnabled && HasDocument)
        {
            _fileWatcherService.Watch(CurrentFilePath);
        }
        else
        {
            _fileWatcherService.Stop();
        }
    }

    private void ToggleSidebar()
    {
        if (IsCompactMode)
        {
            return;
        }

        _isSidebarOpen = !_isSidebarOpen;
        AnimateSidebar(_isSidebarOpen ? 286 : 0);
    }

    private void ToggleCompactMode()
    {
        if (!IsCompactMode)
        {
            _sidebarWasOpenBeforeCompact = _isSidebarOpen;
            if (IsEditing)
            {
                RenderCurrentMarkdownPreview();
                IsEditing = false;
            }

            IsCompactMode = true;
            AnimateSidebar(0);
            return;
        }

        IsCompactMode = false;
        _isSidebarOpen = _sidebarWasOpenBeforeCompact;
        AnimateSidebar(_isSidebarOpen ? 286 : 0);
    }

    private void AnimateSidebar(double target)
    {
        var start = SidebarWidth.Value;
        _tweenService.Animate(start, target, TimeSpan.FromMilliseconds(220), value =>
        {
            SidebarWidth = new GridLength(value);
            SidebarGapWidth = new GridLength(Math.Max(0, 14 * (value / 286)));
            SidebarOpacity = target == 0 ? Math.Max(0, value / 286) : Math.Min(1, value / 286);
        });
    }

    private void ToggleTheme()
    {
        SelectedThemeMode = SelectedThemeMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
    }

    private void ZoomIn() => SetZoom(ZoomPercent + 10);

    private void ZoomOut() => SetZoom(ZoomPercent - 10);

    private void ResetZoom() => SetZoom(100);

    private void ToggleAlwaysOnTop()
    {
        IsAlwaysOnTop = !IsAlwaysOnTop;
        _appSettingsService.Update(settings => settings.IsAlwaysOnTop = IsAlwaysOnTop);
    }

    private void SetZoom(int zoomPercent)
    {
        var normalized = NormalizeZoom(zoomPercent);
        if (normalized == ZoomPercent)
        {
            return;
        }

        ZoomPercent = normalized;
        _themeService.ApplyZoom(normalized);
        _appSettingsService.Update(settings => settings.ZoomPercent = normalized);
        _ = ReloadAsync();
    }

    private static int NormalizeZoom(int zoomPercent)
    {
        if (zoomPercent <= 0)
        {
            return 100;
        }

        return Math.Clamp((int)Math.Round(zoomPercent / 10d) * 10, 50, 200);
    }

    private void ToggleEdit()
    {
        if (IsEditing)
        {
            RenderCurrentMarkdownPreview();
            IsEditing = false;
            return;
        }

        IsEditing = true;
        UpdateEditorPreviewLayout();
        RenderEditorPreview();
    }

    private void UpdateEditorPreviewLayout()
    {
        if (SelectedEditorMode == EditorMode.SplitPreview)
        {
            EditorPreviewGapWidth = new GridLength(14);
            EditorPreviewWidth = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            EditorPreviewGapWidth = new GridLength(0);
            EditorPreviewWidth = new GridLength(0);
        }
    }

    private void ScheduleEditorPreviewRender()
    {
        if (!IsEditing || SelectedEditorMode != EditorMode.SplitPreview)
        {
            return;
        }

        _editorPreviewTimer.Stop();
        _editorPreviewTimer.Start();
    }

    private void RenderEditorPreview()
    {
        if (!HasDocument || SelectedEditorMode != EditorMode.SplitPreview)
        {
            EditorPreviewDocument = null;
            return;
        }

        var rendered = _rendererService.Render(MarkdownText, GetCurrentBaseDirectory());
        EditorPreviewDocument = rendered.Document;
    }

    private void RenderCurrentMarkdownPreview()
    {
        if (!HasDocument)
        {
            return;
        }

        try
        {
            RenderStatus = Localize("PreviewUpdated");
            var rendered = _rendererService.Render(MarkdownText, GetCurrentBaseDirectory());
            Document = rendered.Document;

            TableOfContents.Clear();
            foreach (var item in rendered.TableOfContents)
            {
                TableOfContents.Add(item);
            }

            DocumentInfo =
                $"{Localize("InfoStatus")}: {Localize("PreviewState")}\n" +
                $"{Localize("InfoLines")}: {rendered.LineCount:N0}\n" +
                $"{Localize("InfoWords")}: {rendered.WordCount:N0}\n" +
                $"{Localize("InfoHeadings")}: {rendered.HeadingCount:N0}";
        }
        catch (Exception ex)
        {
            RenderStatus = Localize("PreviewFailed");
            Document = new FlowDocument(new Paragraph(new Run(ex.Message)));
        }
    }

    private string GetCurrentBaseDirectory()
    {
        return Path.GetDirectoryName(CurrentFilePath) ?? string.Empty;
    }

    private string BuildDocumentInfo(FileInfo fileInfo, RenderedMarkdownDocument rendered)
    {
        return
            $"{Localize("InfoSize")}: {fileInfo.Length:N0} bytes\n" +
            $"{Localize("InfoLines")}: {rendered.LineCount:N0}\n" +
            $"{Localize("InfoWords")}: {rendered.WordCount:N0}\n" +
            $"{Localize("InfoHeadings")}: {rendered.HeadingCount:N0}\n" +
            $"{Localize("InfoModified")}: {fileInfo.LastWriteTime:g}";
    }

    private void RefreshLocalizedOptions()
    {
        ReplaceOptions(ThemeModeOptions,
        [
            new(ThemeMode.Light, Localize("ThemeLight")),
            new(ThemeMode.Dark, Localize("ThemeDark")),
            new(ThemeMode.System, Localize("ThemeSystem"))
        ]);
        ReplaceOptions(TypographyPresetOptions,
        [
            new(TypographyPreset.Comfortable, Localize("TypographyComfortable")),
            new(TypographyPreset.Compact, Localize("TypographyCompact")),
            new(TypographyPreset.Editorial, Localize("TypographyEditorial"))
        ]);
        ReplaceOptions(EditorModeOptions,
        [
            new(EditorMode.Markdown, Localize("EditorMarkdown")),
            new(EditorMode.SplitPreview, Localize("EditorSplit"))
        ]);
        ReplaceOptions(LanguageOptions,
        [
            new(AppLanguage.Korean, Localize("LanguageKorean")),
            new(AppLanguage.English, Localize("LanguageEnglish")),
            new(AppLanguage.Japanese, Localize("LanguageJapanese"))
        ]);
    }

    private void RefreshLocalizedContent()
    {
        if (!HasDocument)
        {
            DocumentTitle = Localize("Untitled");
            DocumentSubtitle = Localize("NoOpenFile");
            DocumentInfo = Localize("NoDocumentInfo");
        }

        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(DirtyLabel));
    }

    private static void ReplaceOptions<T>(ObservableCollection<SelectionOption<T>> target, IEnumerable<SelectionOption<T>> options)
    {
        var replacements = options.ToList();
        var canUpdateInPlace = target.Count == replacements.Count
            && target.Select(option => option.Value).SequenceEqual(replacements.Select(option => option.Value));

        if (canUpdateInPlace)
        {
            for (var index = 0; index < target.Count; index++)
            {
                target[index].Label = replacements[index].Label;
            }
            return;
        }

        target.Clear();
        foreach (var replacement in replacements)
        {
            target.Add(replacement);
        }
    }
}
