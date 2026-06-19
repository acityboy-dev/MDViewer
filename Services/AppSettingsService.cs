using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MDViewer.Models;

namespace MDViewer.Services;

public sealed class AppSettingsService
{
    private readonly object _syncRoot = new();
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "MarkFlow");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; }

    public void Update(Action<AppSettings> update)
    {
        lock (_syncRoot)
        {
            update(Current);
            Save();
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _jsonOptions)
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void Save()
    {
        var temporaryPath = _settingsPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Current, _jsonOptions));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
