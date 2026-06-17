using System.Diagnostics;

namespace MDViewer.Services;

public sealed class NavigationService
{
    public void OpenExternalLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
