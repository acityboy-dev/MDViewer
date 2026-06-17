using System.Windows.Documents;

namespace MDViewer.Models;

public sealed class RenderedMarkdownDocument
{
    public FlowDocument Document { get; init; } = new();
    public IReadOnlyList<TableOfContentsItem> TableOfContents { get; init; } = Array.Empty<TableOfContentsItem>();
    public int WordCount { get; init; }
    public int LineCount { get; init; }
    public int HeadingCount { get; init; }
}
