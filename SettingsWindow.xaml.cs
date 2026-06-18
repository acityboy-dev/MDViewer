using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using MDViewer.Services;
using MDViewer.ViewModels;

namespace MDViewer;

public partial class SettingsWindow : Window
{
    private readonly DwmBackdropService _dwmBackdropService = new();
    private MainViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        ApplyWindowTheme();
        _dwmBackdropService.ApplyRoundedCorners(this, true);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            ApplyWindowTheme();
        }
    }

    private void ApplyWindowTheme()
    {
        var isDark = _viewModel?.IsDarkTheme == true;
        _dwmBackdropService.Apply(this, isDark);
        _dwmBackdropService.ApplyDarkCaption(this, isDark);
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
