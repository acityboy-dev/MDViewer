using System.Globalization;
using System.Windows;
using MDViewer.Models;

namespace MDViewer.Services;

public sealed class LanguageService
{
    private readonly AppSettingsService _settingsService;

    public LanguageService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Korean;

    public void Initialize()
    {
        ApplyLanguage(_settingsService.Current.Language, save: false);
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
            _settingsService.Update(settings => settings.Language = language);
        }
    }

    public string Get(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }

}
