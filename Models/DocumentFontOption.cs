namespace MDViewer.Models;

public sealed class DocumentFontOption
{
    public DocumentFontOption(string id, string label, string familyName, string fallbackFamily, params string[] downloadUrls)
    {
        Id = id;
        Label = label;
        FamilyName = familyName;
        FallbackFamily = fallbackFamily;
        DownloadUrls = downloadUrls;
    }

    public string Id { get; }
    public string Label { get; }
    public string FamilyName { get; }
    public string FallbackFamily { get; }
    public IReadOnlyList<string> DownloadUrls { get; }
}
