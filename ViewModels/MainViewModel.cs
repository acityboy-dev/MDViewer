using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
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
    private Window? _window;
    private string _currentFilePath = string.Empty;
    private string _documentTitle = "제목 없음";
    private string _documentSubtitle = "열린 파일이 없습니다";
    private string _documentInfo = "Markdown 문서를 열면 문서 정보가 표시됩니다.";
    private string _renderStatus = string.Empty;
    private string _markdownText = string.Empty;
    private FlowDocument? _document;
    private GridLength _sidebarWidth = new(286);
    private double _sidebarOpacity = 1;
    private bool _isSidebarOpen = true;
    private bool _isEditing;
    private bool _isDirty;
    private bool _isFileWatchingEnabled = true;
    private ThemeMode _selectedThemeMode = ThemeMode.Light;
    private TypographyPreset _selectedTypographyPreset = TypographyPreset.Comfortable;
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
        DwmBackdropService dwmBackdropService)
    {
        _rendererService = rendererService;
        _themeService = themeService;
        _fontCacheService = fontCacheService;
        _recentFileService = recentFileService;
        _fileWatcherService = fileWatcherService;
        _tweenService = tweenService;
        _dwmBackdropService = dwmBackdropService;

        OpenFileCommand = new AsyncRelayCommand(OpenFileWithDialogAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => HasDocument);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasDocument && IsDirty);
        ToggleEditCommand = new RelayCommand(ToggleEdit, () => HasDocument);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);

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
    public IReadOnlyList<SelectionOption<ThemeMode>> ThemeModeOptions { get; } =
    [
        new(ThemeMode.Light, "라이트"),
        new(ThemeMode.Dark, "다크"),
        new(ThemeMode.System, "시스템")
    ];

    public IReadOnlyList<SelectionOption<TypographyPreset>> TypographyPresetOptions { get; } =
    [
        new(TypographyPreset.Comfortable, "편안하게"),
        new(TypographyPreset.Compact, "촘촘하게"),
        new(TypographyPreset.Editorial, "매거진")
    ];

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
    public RelayCommand ToggleThemeCommand { get; }

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

    public bool HasDocument => !string.IsNullOrWhiteSpace(CurrentFilePath);

    public string MarkdownText
    {
        get => _markdownText;
        set
        {
            if (SetProperty(ref _markdownText, value) && HasDocument)
            {
                IsDirty = true;
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

    public string ModeLabel => IsEditing ? "편집" : "보기";

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

    public string DirtyLabel => IsDirty ? "수정됨" : "저장됨";

    public GridLength SidebarWidth
    {
        get => _sidebarWidth;
        private set => SetProperty(ref _sidebarWidth, value);
    }

    public double SidebarOpacity
    {
        get => _sidebarOpacity;
        private set => SetProperty(ref _sidebarOpacity, value);
    }

    public bool IsFileWatchingEnabled
    {
        get => _isFileWatchingEnabled;
        set
        {
            if (SetProperty(ref _isFileWatchingEnabled, value))
            {
                UpdateWatcher();
            }
        }
    }

    public ThemeMode SelectedThemeMode
    {
        get => _selectedThemeMode;
        set
        {
            if (SetProperty(ref _selectedThemeMode, value))
            {
                _themeService.ApplyTheme(value);
                if (_window is not null)
                {
                    _dwmBackdropService.ApplyDarkCaption(_window, value == ThemeMode.Dark);
                }
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
                _ = ApplyDocumentFontAsync(value);
            }
        }
    }

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
        _dwmBackdropService.ApplyDarkCaption(window, SelectedThemeMode == ThemeMode.Dark);
        SelectedDocumentFontOption ??= DocumentFontOptions.FirstOrDefault(option => option.Id == "system");
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
        _fileWatcherService.Dispose();
    }

    private async Task OpenFileWithDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Markdown 열기",
            Filter = "Markdown 파일 (*.md;*.markdown)|*.md;*.markdown|모든 파일 (*.*)|*.*",
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
            RenderStatus = "File not found";
            return;
        }

        try
        {
            RenderStatus = "렌더링 중...";
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
            DocumentInfo =
                $"크기: {fileInfo.Length:N0} bytes\n" +
                $"줄 수: {rendered.LineCount:N0}\n" +
                $"단어 수: {rendered.WordCount:N0}\n" +
                $"제목 수: {rendered.HeadingCount:N0}\n" +
                $"수정일: {fileInfo.LastWriteTime:g}";

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
            RenderStatus = "렌더링 실패";
            Document = new FlowDocument(new Paragraph(new Run(ex.Message)));
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
        RenderStatus = "저장됨";
        await LoadFileAsync(CurrentFilePath, addToRecent: false);
    }

    private async Task ReloadAsync()
    {
        if (HasDocument)
        {
            await LoadFileAsync(CurrentFilePath, addToRecent: false);
        }
    }

    private async Task ApplyDocumentFontAsync(DocumentFontOption option)
    {
        RenderStatus = option.DownloadUrls.Count == 0 ? string.Empty : "폰트 준비 중...";
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
        _isSidebarOpen = !_isSidebarOpen;
        var target = _isSidebarOpen ? 286 : 0;
        var start = SidebarWidth.Value;
        _tweenService.Animate(start, target, TimeSpan.FromMilliseconds(220), value =>
        {
            SidebarWidth = new GridLength(value);
            SidebarOpacity = target == 0 ? Math.Max(0, value / 286) : Math.Min(1, value / 286);
        });
    }

    private void ToggleTheme()
    {
        SelectedThemeMode = SelectedThemeMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
    }

    private void ToggleEdit()
    {
        IsEditing = !IsEditing;
    }
}
