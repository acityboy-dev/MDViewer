using System.Windows;
using System.Windows.Media;
using ThemeMode = MDViewer.Models.ThemeMode;
using TypographyPreset = MDViewer.Models.TypographyPreset;

namespace MDViewer.Services;

public sealed class ThemeService
{
    public ThemeMode CurrentTheme { get; private set; } = ThemeMode.Light;
    public bool IsDarkTheme { get; private set; }
    public TypographyPreset CurrentTypography { get; private set; } = TypographyPreset.Comfortable;
    public FontFamily CurrentDocumentFontFamily { get; private set; } = new("Malgun Gothic, Segoe UI");
    public int ZoomPercent { get; private set; } = 100;

    public void ApplyTheme(ThemeMode themeMode)
    {
        CurrentTheme = themeMode;
        var effectiveTheme = themeMode == ThemeMode.System ? GetSystemTheme() : themeMode;
        IsDarkTheme = effectiveTheme == ThemeMode.Dark;
        var source = new Uri($"Resources/Theme.{effectiveTheme}.xaml", UriKind.Relative);
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var dictionarySource = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (dictionarySource.Contains("Theme.", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Insert(0, new ResourceDictionary { Source = source });
    }

    public void ApplyTypography(TypographyPreset preset)
    {
        CurrentTypography = preset;
    }

    public void ApplyDocumentFont(FontFamily fontFamily)
    {
        CurrentDocumentFontFamily = fontFamily;
    }

    public void ApplyZoom(int zoomPercent)
    {
        ZoomPercent = Math.Clamp(zoomPercent, 50, 200);
    }

    public Brush GetBrush(string key)
    {
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public double BodyFontSize => Scale(CurrentTypography switch
        {
            TypographyPreset.Compact => 14,
            TypographyPreset.Editorial => 17,
            _ => 15.5
        });

    public double DocumentLineHeight => Scale(CurrentTypography switch
        {
            TypographyPreset.Compact => 22,
            TypographyPreset.Editorial => 30,
            _ => 26
        });

    public double CodeFontSize => Scale(CurrentTypography switch
    {
        TypographyPreset.Compact => 13,
        TypographyPreset.Editorial => 16,
        _ => 14.5
    });

    public double Scale(double value) => value * ZoomPercent / 100d;

    private static ThemeMode GetSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            return ThemeMode.Light;
        }
    }
}
