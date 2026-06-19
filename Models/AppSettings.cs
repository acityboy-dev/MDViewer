namespace MDViewer.Models;

public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.Korean;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public TypographyPreset Typography { get; set; } = TypographyPreset.Comfortable;
    public EditorMode EditorMode { get; set; } = EditorMode.Markdown;
    public string DocumentFontId { get; set; } = "system";
    public bool IsFileWatchingEnabled { get; set; } = true;
}
