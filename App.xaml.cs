using System.Windows;
using MDViewer.Services;

namespace MDViewer;

public partial class App : Application
{
    public static LanguageService LanguageService { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        LanguageService.Initialize();
        base.OnStartup(e);
    }
}
