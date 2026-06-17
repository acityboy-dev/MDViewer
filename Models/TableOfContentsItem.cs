using System.Windows;
using System.Windows.Documents;

namespace MDViewer.Models;

public sealed class TableOfContentsItem
{
    public string Title { get; init; } = string.Empty;
    public int Level { get; init; }
    public Block? TargetBlock { get; init; }
    public Thickness Indent => new(Math.Max(0, Level - 1) * 12, 4, 0, 4);
}
