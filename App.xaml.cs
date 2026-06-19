using System.Windows;
using MDViewer.Services;

namespace MDViewer;

public partial class App : Application
{
    public static AppSettingsService SettingsService { get; } = new();
    public static LanguageService LanguageService { get; } = new(SettingsService);

    protected override void OnStartup(StartupEventArgs e)
    {
        LanguageService.Initialize();
        base.OnStartup(e);
    }
}
