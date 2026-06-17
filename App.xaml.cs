using System.Windows;
using System.Globalization;
using System.Threading;

namespace MDViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var culture = CultureInfo.GetCultureInfo("ko-KR");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        base.OnStartup(e);
    }
}
