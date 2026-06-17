using System.Windows;
using System.Windows.Input;
using MDViewer.Services;

namespace MDViewer;

public partial class SettingsWindow : Window
{
    private readonly DwmBackdropService _dwmBackdropService = new();

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _dwmBackdropService.Apply(this);
        _dwmBackdropService.ApplyRoundedCorners(this, true);
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
