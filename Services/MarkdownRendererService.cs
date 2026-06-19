using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MDViewer.Models;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableRow = System.Windows.Documents.TableRow;
using WpfTableRowGroup = System.Windows.Documents.TableRowGroup;

namespace MDViewer.Services;

public sealed partial class MarkdownRendererService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ThemeService _themeService;
    private readonly Dictionary<string, RenderedMarkdownDocument> _cache = new(StringComparer.OrdinalIgnoreCase);

    public MarkdownRendererService(ThemeService themeService)
    {
        _themeService = themeService;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseTaskLists()
            .Build();
    }

    public async Task<RenderedMarkdownDocument> RenderFileAsync(string path)
    {
        var info = new FileInfo(path);
        var key = $"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{_themeService.CurrentTypography}|{_themeService.CurrentTheme}|{_themeService.CurrentDocumentFontFamily.Source}|{_themeService.ZoomPercent}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var markdown = await File.ReadAllTextAsync(path);
        var result = Render(markdown, Path.GetDirectoryName(path) ?? string.Empty);
        _cache.Clear();
        _cache[key] = result;
        return result;
    }

    public RenderedMarkdownDocument Render(string markdown, string baseDirectory)
    {
        var markdownDocument = Markdig.Markdown.Parse(markdown, _pipeline);
        var toc = new List<TableOfContentsItem>();
        var flowDocument = CreateDocument();

        foreach (var block in markdownDocument)
        {
            var element = RenderBlock(block, toc, baseDirectory);
            if (element is not null)
            {
                flowDocument.Blocks.Add(element);
            }
        }

        return new RenderedMarkdownDocument
        {
            Document = flowDocument,
            TableOfContents = toc,
            WordCount = WordRegex().Matches(markdown).Count,
            LineCount = markdown.Split('\n').Length,
            HeadingCount = toc.Count
        };
    }

    private FlowDocument CreateDocument()
    {
        return new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = _themeService.CurrentDocumentFontFamily,
            FontSize = _themeService.BodyFontSize,
            LineHeight = _themeService.DocumentLineHeight,
            Foreground = _themeService.GetBrush("TextPrimaryBrush"),
            Background = System.Windows.Media.Brushes.Transparent,
            ColumnWidth = 880
        };
    }

    private System.Windows.Documents.Block? RenderBlock(MarkdigBlock block, ICollection<TableOfContentsItem> toc, string baseDirectory)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading, toc, baseDirectory),
            ParagraphBlock paragraph => RenderParagraph(paragraph.Inline, baseDirectory),
            QuoteBlock quote => RenderQuote(quote, toc, baseDirectory),
            FencedCodeBlock code => RenderCodeBlock(code.Lines.ToString(), code.Info ?? string.Empty),
            CodeBlock code => RenderCodeBlock(code.Lines.ToString(), string.Empty),
            ListBlock list => RenderList(list, toc, baseDirectory),
            ThematicBreakBlock => RenderHorizontalRule(),
            Markdig.Extensions.Tables.Table table => RenderTable(table, toc, baseDirectory),
            HtmlBlock html => RenderCodeBlock(html.Lines.ToString(), "html"),
            _ => null
        };
    }

    private Paragraph RenderHeading(HeadingBlock heading, ICollection<TableOfContentsItem> toc, string baseDirectory)
    {
        var paragraph = RenderParagraph(heading.Inline, baseDirectory);
        var text = ExtractInlineText(heading.Inline);
        toc.Add(new TableOfContentsItem { Title = text, Level = heading.Level, TargetBlock = paragraph });

        paragraph.Margin = new Thickness(0, heading.Level == 1 ? 18 : 16, 0, 8);
        paragraph.FontWeight = FontWeights.SemiBold;
        paragraph.LineHeight = double.NaN;
        paragraph.FontSize = heading.Level switch
        {
            1 => _themeService.Scale(34),
            2 => _themeService.Scale(26),
            3 => _themeService.Scale(21),
            4 => _themeService.Scale(18),
            _ => _themeService.Scale(16)
        };
        return paragraph;
    }

    private Paragraph RenderParagraph(ContainerInline? inline, string baseDirectory)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = _themeService.GetBrush("TextPrimaryBrush")
        };

        AppendInlines(paragraph.Inlines, inline, baseDirectory);
        return paragraph;
    }

    private System.Windows.Documents.Block RenderQuote(QuoteBlock quote, ICollection<TableOfContentsItem> toc, string baseDirectory)
    {
        var section = new Section
        {
            Margin = new Thickness(0, 8, 0, 14),
            BorderThickness = new Thickness(4, 0, 0, 0),
            BorderBrush = _themeService.GetBrush("QuoteStripeBrush"),
            Padding = new Thickness(14, 4, 0, 4),
            Foreground = _themeService.GetBrush("TextSecondaryBrush")
        };

        foreach (var child in quote)
        {
            var rendered = RenderBlock(child, toc, baseDirectory);
            if (rendered is not null)
            {
                section.Blocks.Add(rendered);
            }
        }

        return section;
    }

    private System.Windows.Documents.Block RenderCodeBlock(string code, string language)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 8, 0, 14),
            Padding = new Thickness(14),
            Background = _themeService.GetBrush("CodeBackgroundBrush"),
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"),
            FontSize = _themeService.CodeFontSize,
            LineHeight = _themeService.Scale(22)
        };

        if (!string.IsNullOrWhiteSpace(language))
        {
            paragraph.Inlines.Add(new Run(language.Trim())
            {
                FontSize = _themeService.Scale(11),
                FontWeight = FontWeights.SemiBold,
                Foreground = _themeService.GetBrush("TextMutedBrush")
            });
            paragraph.Inlines.Add(new LineBreak());
        }

        paragraph.Inlines.Add(new Run(code.TrimEnd()));
        return paragraph;
    }

    private System.Windows.Documents.Block RenderList(ListBlock list, ICollection<TableOfContentsItem> toc, string baseDirectory)
    {
        var flowList = new List
        {
            MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(18, 0, 0, 12),
            Padding = new Thickness(18, 0, 0, 0)
        };

        foreach (var itemBlock in list.OfType<ListItemBlock>())
        {
            var item = new ListItem();
            foreach (var child in itemBlock)
            {
                var rendered = RenderBlock(child, toc, baseDirectory);
                if (rendered is not null)
                {
                    item.Blocks.Add(rendered);
                }
            }

            flowList.ListItems.Add(item);
        }

        return flowList;
    }

    private System.Windows.Documents.Block RenderHorizontalRule()
    {
        return new BlockUIContainer(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 10, 0, 18),
            Background = _themeService.GetBrush("HairlineBrush")
        });
    }

    private System.Windows.Documents.Block RenderTable(Markdig.Extensions.Tables.Table table, ICollection<TableOfContentsItem> toc, string baseDirectory)
    {
        var wpfTable = new WpfTable
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 8, 0, 18)
        };
        var rowGroup = new WpfTableRowGroup();
        wpfTable.RowGroups.Add(rowGroup);

        foreach (var row in table.OfType<MarkdigTableRow>())
        {
            var wpfRow = new WpfTableRow();
            foreach (var cell in row.OfType<MarkdigTableCell>())
            {
                var wpfCell = new WpfTableCell
                {
                    BorderBrush = _themeService.GetBrush("HairlineBrush"),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 8, 10, 8),
                    Background = row.IsHeader ? _themeService.GetBrush("AccentSoftBrush") : null
                };

                foreach (var child in cell)
                {
                    var rendered = RenderBlock(child, toc, baseDirectory);
                    if (rendered is not null)
                    {
                        wpfCell.Blocks.Add(rendered);
                    }
                }

                wpfRow.Cells.Add(wpfCell);
            }

            rowGroup.Rows.Add(wpfRow);
        }

        return wpfTable;
    }

    private void AppendInlines(InlineCollection target, ContainerInline? container, string baseDirectory)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(target, inline, baseDirectory);
        }
    }

    private void AppendInline(InlineCollection target, MarkdigInline inline, string baseDirectory)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run(literal.Content.ToString()));
                break;
            case LineBreakInline:
                target.Add(new LineBreak());
                break;
            case CodeInline code:
                target.Add(new Run(code.Content)
                {
                    FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"),
                    Background = _themeService.GetBrush("CodeBackgroundBrush"),
                    FontSize = _themeService.CodeFontSize
                });
                break;
            case EmphasisInline emphasis:
                var span = new Span();
                if (emphasis.DelimiterCount >= 2)
                {
                    span.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    span.FontStyle = FontStyles.Italic;
                }
                AppendInlines(span.Inlines, emphasis, baseDirectory);
                target.Add(span);
                break;
            case LinkInline { IsImage: true } image:
                AddImage(target, image, baseDirectory);
                break;
            case LinkInline link:
                var hyperlink = new Hyperlink
                {
                    NavigateUri = TryCreateUri(link.Url, baseDirectory),
                    Foreground = _themeService.GetBrush("AccentBrush")
                };
                AppendInlines(hyperlink.Inlines, link, baseDirectory);
                hyperlink.RequestNavigate += (_, args) =>
                {
                    if (args.Uri is not null)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.AbsoluteUri)
                        {
                            UseShellExecute = true
                        });
                    }
                };
                target.Add(hyperlink);
                break;
            default:
                if (inline is ContainerInline nested)
                {
                    AppendInlines(target, nested, baseDirectory);
                }
                break;
        }
    }

    private void AddImage(InlineCollection target, LinkInline imageInline, string baseDirectory)
    {
        var uri = TryCreateUri(imageInline.Url, baseDirectory);
        if (uri is null)
        {
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = uri;
            bitmap.DecodePixelWidth = 1200;
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new Image
            {
                Source = bitmap,
                MaxWidth = 820,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(0, 8, 0, 12)
            };
            target.Add(new InlineUIContainer(image));
        }
        catch
        {
            target.Add(new Run($"[image: {imageInline.Url}]")
            {
                Foreground = _themeService.GetBrush("TextMutedBrush")
            });
        }
    }

    private static Uri? TryCreateUri(string? url, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var path = Path.GetFullPath(Path.Combine(baseDirectory, url));
        return File.Exists(path) ? new Uri(path) : null;
    }

    private static string ExtractInlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var writer = new StringWriter();
        foreach (var child in inline)
        {
            if (child is LiteralInline literal)
            {
                writer.Write(literal.Content);
            }
            else if (child is CodeInline code)
            {
                writer.Write(code.Content);
            }
            else if (child is ContainerInline nested)
            {
                writer.Write(ExtractInlineText(nested));
            }
        }

        return writer.ToString();
    }

    [GeneratedRegex(@"\b[\p{L}\p{N}_'-]+\b", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
