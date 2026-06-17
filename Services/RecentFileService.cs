using System.IO;
using System.Text.Json;
using MDViewer.Models;

namespace MDViewer.Services;

public sealed class RecentFileService
{
    private const int MaxItems = 12;
    private readonly string _storePath;

    public RecentFileService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "MDViewer");
        Directory.CreateDirectory(folder);
        _storePath = Path.Combine(folder, "recent-files.json");
    }

    public IReadOnlyList<RecentFileEntry> Load()
    {
        if (!File.Exists(_storePath))
        {
            return Array.Empty<RecentFileEntry>();
        }

        try
        {
            var json = File.ReadAllText(_storePath);
            return JsonSerializer.Deserialize<List<RecentFileEntry>>(json)?
                .Where(item => File.Exists(item.FullPath))
                .OrderByDescending(item => item.LastOpenedUtc)
                .Take(MaxItems)
                .ToList() ?? [];
        }
        catch
        {
            return Array.Empty<RecentFileEntry>();
        }
    }

    public void Add(string path)
    {
        var items = Load()
            .Where(item => !string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentFileEntry { FullPath = path, LastOpenedUtc = DateTime.UtcNow })
            .Take(MaxItems)
            .ToList();

        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_storePath, json);
    }
}
