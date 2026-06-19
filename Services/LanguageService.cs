using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using MDViewer.Models;

namespace MDViewer.Services;

public sealed class LanguageService
{
    private readonly string _settingsPath;

    public LanguageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "MarkFlow");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Korean;

    public void Initialize()
    {
        ApplyLanguage(LoadLanguage(), save: false);
    }

    public void ApplyLanguage(AppLanguage language, bool save = true)
    {
        CurrentLanguage = language;
        var cultureName = language switch
        {
            AppLanguage.Korean => "ko-KR",
            AppLanguage.English => "en-US",
            AppLanguage.Japanese => "ja-JP",
            _ => "ko-KR"
        };
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Strings.", StringComparison.OrdinalIgnoreCase) == true);
        var index = existing is null ? 0 : dictionaries.IndexOf(existing);
        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        var suffix = language switch
        {
            AppLanguage.Korean => "Ko",
            AppLanguage.English => "En",
            AppLanguage.Japanese => "Ja",
            _ => "Ko"
        };
        dictionaries.Insert(index, new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{suffix}.xaml", UriKind.Relative)
        });

        if (save)
        {
            SaveLanguage(language);
        }
    }

    public string Get(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }

    private AppLanguage LoadLanguage()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return AppLanguage.Korean;
            }

            var settings = JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(_settingsPath));
            return Enum.TryParse<AppLanguage>(settings?.Language, out var language)
                ? language
                : AppLanguage.Korean;
        }
        catch
        {
            return AppLanguage.Korean;
        }
    }

    private void SaveLanguage(AppLanguage language)
    {
        var settings = new LanguageSettings { Language = language.ToString() };
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private sealed class LanguageSettings
    {
        public string Language { get; set; } = AppLanguage.Korean.ToString();
    }
}
