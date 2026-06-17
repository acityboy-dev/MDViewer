using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows.Media;
using MDViewer.Models;

namespace MDViewer.Services;

public sealed class FontCacheService
{
    private const int FrPrivate = 0x10;
    private readonly string _fontCacheDirectory;
    private readonly HttpClient _httpClient = new();

    public FontCacheService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _fontCacheDirectory = Path.Combine(appData, "MDViewer", "Fonts");
        Directory.CreateDirectory(_fontCacheDirectory);
    }

    public async Task<FontFamily> EnsureFontAsync(DocumentFontOption option)
    {
        if (option.DownloadUrls.Count == 0)
        {
            return new FontFamily(option.FallbackFamily);
        }

        var fontPath = Path.Combine(_fontCacheDirectory, $"{option.Id}.ttf");
        if (!File.Exists(fontPath))
        {
            await DownloadFirstAvailableAsync(option, fontPath);
        }

        if (!File.Exists(fontPath))
        {
            return new FontFamily(option.FallbackFamily);
        }

        TryLoadPrivateFont(fontPath);
        var familyName = ResolveFamilyName(fontPath, option.FamilyName);
        var baseUri = new Uri(Path.GetDirectoryName(fontPath) + Path.DirectorySeparatorChar, UriKind.Absolute);
        return new FontFamily(baseUri, $"./#{familyName}");
    }

    private async Task DownloadFirstAvailableAsync(DocumentFontOption option, string fontPath)
    {
        var tempPath = fontPath + ".download";
        foreach (var url in option.DownloadUrls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(tempPath);
                await source.CopyToAsync(target);
                target.Close();

                if (new FileInfo(tempPath).Length < 1024)
                {
                    File.Delete(tempPath);
                    continue;
                }

                File.Move(tempPath, fontPath, overwrite: true);
                return;
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }

    private static void TryLoadPrivateFont(string path)
    {
        try
        {
            AddFontResourceEx(path, FrPrivate, IntPtr.Zero);
        }
        catch
        {
        }
    }

    private static string ResolveFamilyName(string path, string fallbackName)
    {
        try
        {
            var glyphTypeface = new GlyphTypeface(new Uri(path, UriKind.Absolute));
            if (glyphTypeface.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("ko-KR"), out var koreanName))
            {
                return koreanName;
            }

            if (glyphTypeface.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out var englishName))
            {
                return englishName;
            }

            var firstName = glyphTypeface.FamilyNames.Values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                return firstName;
            }
        }
        catch
        {
        }

        return fallbackName;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string name, int flags, IntPtr reserved);
}
