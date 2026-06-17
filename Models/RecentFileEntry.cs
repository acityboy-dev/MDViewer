using System.IO;

namespace MDViewer.Models;

public sealed class RecentFileEntry
{
    public string FullPath { get; set; } = string.Empty;
    public DateTime LastOpenedUtc { get; set; }
    public string DisplayName => Path.GetFileName(FullPath);
}
