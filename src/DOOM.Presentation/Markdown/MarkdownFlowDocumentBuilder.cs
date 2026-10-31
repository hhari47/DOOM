namespace DOOM.Presentation.Markdown;

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DOOM.Presentation.Themes;

/// <summary>
/// Converts parsed markdown blocks into WPF FlowDocument and Inline elements.
/// Supports incremental block updating during streaming to avoid full document rebuilds.
/// </summary>
public static class MarkdownFlowDocumentBuilder
{
    private static readonly FontFamily MonoFont = new("Consolas, Cascadia Mono, monospace");

    public static FlowDocument CreateDocument(IReadOnlyList<MarkdownBlock> blocks)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = DoomColors.TextPrimaryBrush,
            FontSize = 13.5,
            LineHeight = 20,
            FontFamily = new FontFamily("Segoe UI, Aptos, sans-serif")
        };

        foreach (var block in blocks)
        {
            doc.Blocks.Add(BuildBlock(block));
        }

        return doc;
    }

    /// <summary>
    /// Incrementally updates the FlowDocument with new blocks, reusing existing blocks where possible.
    /// </summary>
    public static void UpdateDocumentIncremental(FlowDocument doc, IReadOnlyList<MarkdownBlock> newBlocks)
    {
        if (doc == null || newBlocks == null)
        {
            return;
        }

        int commonCount = Math.Min(doc.Blocks.Count, newBlocks.Count);

        // Update the last overlapping block if needed (in case the active streaming line received more tokens)
        if (commonCount > 0)
        {
            int tailIndex = commonCount - 1;
            var updatedTail = BuildBlock(newBlocks[tailIndex]);

            // Replace only the tail block that was actively receiving tokens
            var currentBlocks = doc.Blocks.ToList();
            doc.Blocks.Remove(currentBlocks[tailIndex]);
            if (tailIndex < doc.Blocks.Count)
            {
                doc.Blocks.InsertBefore(doc.Blocks.ElementAt(tailIndex), updatedTail);
            }
            else
            {
                doc.Blocks.Add(updatedTail);
            }
        }

        // Add any newly completed lines beyond the previous count
        for (int i = commonCount; i < newBlocks.Count; i++)
        {
            doc.Blocks.Add(BuildBlock(newBlocks[i]));
        }
    }

    public static Block BuildBlock(MarkdownBlock block)
    {
        var p = new Paragraph
        {
            Margin = block.Type switch
            {
                MarkdownBlockType.BlankLine => new Thickness(0, 4, 0, 4),
                MarkdownBlockType.BulletItem => new Thickness(12, 1, 0, 1),
                MarkdownBlockType.NumberedItem => new Thickness(12, 1, 0, 1),
                _ => new Thickness(0, 2, 0, 2)
            },
            LineHeight = block.Type == MarkdownBlockType.BlankLine ? 8 : 20
        };

        if (block.Type == MarkdownBlockType.BulletItem)
        {
            p.Inlines.Add(new Run("• ")
            {
                Foreground = DoomColors.AccentTextBrush,
                FontWeight = FontWeights.Bold
            });
        }
        else if (block.Type == MarkdownBlockType.NumberedItem && !string.IsNullOrEmpty(block.Prefix))
        {
            p.Inlines.Add(new Run(block.Prefix)
            {
                Foreground = DoomColors.TextSecondaryBrush,
                FontWeight = FontWeights.Medium
            });
        }

        foreach (var inline in block.Inlines)
        {
            p.Inlines.Add(BuildInline(inline));
        }

        return p;
    }

    public static Inline BuildInline(MarkdownInlineToken token)
    {
        return token.Type switch
        {
            MarkdownTokenType.Bold => new Bold(new Run(token.Text)),
            MarkdownTokenType.Italic => new Italic(new Run(token.Text)),
            MarkdownTokenType.BoldItalic => new Bold(new Italic(new Run(token.Text))),
            MarkdownTokenType.Code => new Run(token.Text)
            {
                FontFamily = MonoFont,
                Background = DoomColors.ControlBrush,
                Foreground = DoomColors.TextPrimaryBrush
            },
            MarkdownTokenType.StrippedLink => new Run(token.Text)
            {
                Foreground = DoomColors.AccentTextBrush,
                TextDecorations = TextDecorations.Underline
            },
            _ => new Run(token.Text)
        };
    }
}

