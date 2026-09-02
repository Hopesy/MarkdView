using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdView.Documents;

namespace MarkdView.Parsing;

/// <summary>
/// Markdig AST 到稳定文档模型的唯一适配边界。
/// Documents 层不引用 Markdig，解析器升级时只需在本适配器内处理差异。
/// </summary>
internal static class MarkdigMarkdownDocumentModelFactory
{
    public static MarkdownDocumentModel Create(string sourceText, MarkdownDocument syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(syntaxTree);

        var blocks = syntaxTree
            .Select(block => CreateBlockModel(sourceText, block))
            .ToArray();
        return new MarkdownDocumentModel(
            sourceText,
            new ReadOnlyCollection<MarkdownBlockModel>(blocks));
    }

    private static MarkdownBlockModel CreateBlockModel(
        string sourceText,
        Block block,
        int? tableColumnIndex = null)
    {
        var kind = block switch
        {
            HeadingBlock => MarkdownBlockKind.Heading,
            ParagraphBlock => MarkdownBlockKind.Paragraph,
            QuoteBlock => MarkdownBlockKind.Quote,
            ListBlock => MarkdownBlockKind.List,
            ListItemBlock => MarkdownBlockKind.ListItem,
            Table => MarkdownBlockKind.Table,
            TableRow => MarkdownBlockKind.TableRow,
            TableCell => MarkdownBlockKind.TableCell,
            MathBlock => MarkdownBlockKind.Math,
            CodeBlock => MarkdownBlockKind.Code,
            ThematicBreakBlock => MarkdownBlockKind.ThematicBreak,
            HtmlBlock => MarkdownBlockKind.Html,
            DefinitionList => MarkdownBlockKind.DefinitionList,
            DefinitionItem => MarkdownBlockKind.DefinitionItem,
            DefinitionTerm => MarkdownBlockKind.DefinitionTerm,
            FootnoteGroup => MarkdownBlockKind.FootnoteGroup,
            Footnote => MarkdownBlockKind.Footnote,
            _ => MarkdownBlockKind.Unknown
        };

        var headingLevel = block is HeadingBlock heading ? heading.Level : 0;
        var language = block is FencedCodeBlock fenced ? fenced.Info : null;
        var isOrdered = block is ListBlock list && list.IsOrdered;
        var orderedStart = block is ListBlock ordered
            && int.TryParse(ordered.OrderedStart, out var start)
            ? start
            : 1;
        var isTableHeader = block is TableRow tableRow && tableRow.IsHeader;
        var columnIndex = block is TableCell tableCell
            ? tableColumnIndex ?? tableCell.ColumnIndex
            : -1;
        var columnSpan = block is TableCell columnCell ? Math.Max(1, columnCell.ColumnSpan) : 1;
        var rowSpan = block is TableCell rowCell ? Math.Max(1, rowCell.RowSpan) : 1;
        IReadOnlyList<MarkdownTableColumnAlignment?> tableColumnAlignments = block is Table table
            ? new ReadOnlyCollection<MarkdownTableColumnAlignment?>(
                table.ColumnDefinitions
                    .Select(column => MapTableColumnAlignment(column.Alignment))
                    .ToArray())
            : Array.Empty<MarkdownTableColumnAlignment?>();

        var children = Array.Empty<MarkdownBlockModel>();
        if (block is ContainerBlock container)
        {
            var childModels = new List<MarkdownBlockModel>();
            var nextTableColumnIndex = 0;
            foreach (var child in container)
            {
                int? childColumnIndex = block is TableRow && child is TableCell
                    ? nextTableColumnIndex
                    : null;
                childModels.Add(CreateBlockModel(sourceText, child, childColumnIndex));
                if (block is TableRow && child is TableCell tableCellChild)
                {
                    nextTableColumnIndex += Math.Max(1, tableCellChild.ColumnSpan);
                }
            }

            children = childModels.ToArray();
        }

        var span = block.Span;
        var startOffset = Math.Clamp(span.Start, 0, sourceText.Length);
        var length = Math.Clamp(span.Length, 0, sourceText.Length - startOffset);
        var source = sourceText.Substring(startOffset, length);

        var inlines = CreateInlineModels(sourceText, block);
        var codeText = kind == MarkdownBlockKind.Code && block is CodeBlock codeBlock
            ? codeBlock.Lines.ToString()
            : null;

        return new MarkdownBlockModel(
            kind,
            source,
            startOffset,
            length,
            headingLevel,
            language,
            isOrdered,
            orderedStart,
            new ReadOnlyCollection<MarkdownBlockModel>(children))
        {
            Inlines = inlines,
            CodeText = codeText,
            IsTableHeader = isTableHeader,
            ColumnIndex = columnIndex,
            ColumnSpan = columnSpan,
            RowSpan = rowSpan,
            TableColumnAlignments = tableColumnAlignments,
            SyntaxType = block.GetType().FullName ?? block.GetType().Name,
            RequiresCompatibilityRenderer = (kind is MarkdownBlockKind.Unknown
                or MarkdownBlockKind.DefinitionList
                or MarkdownBlockKind.DefinitionItem
                or MarkdownBlockKind.DefinitionTerm
                or MarkdownBlockKind.FootnoteGroup
                or MarkdownBlockKind.Footnote
                or MarkdownBlockKind.Math)
                || children.Any(child => child.RequiresCompatibilityRenderer)
                || inlines.Any(RequiresCompatibilityRenderer)
        };
    }

    private static MarkdownTableColumnAlignment? MapTableColumnAlignment(TableColumnAlign? alignment)
        => alignment switch
        {
            TableColumnAlign.Left => MarkdownTableColumnAlignment.Left,
            TableColumnAlign.Center => MarkdownTableColumnAlignment.Center,
            TableColumnAlign.Right => MarkdownTableColumnAlignment.Right,
            _ => null
        };

    private static IReadOnlyList<MarkdownInlineModel> CreateInlineModels(string sourceText, Block block)
    {
        var inlineContainer = block switch
        {
            HeadingBlock heading => heading.Inline,
            ParagraphBlock paragraph => paragraph.Inline,
            _ => null
        };

        if (inlineContainer == null)
        {
            return Array.Empty<MarkdownInlineModel>();
        }

        var result = inlineContainer
            .Select(inline => CreateInlineModel(sourceText, inline))
            .ToArray();
        return new ReadOnlyCollection<MarkdownInlineModel>(result);
    }

    private static MarkdownInlineModel CreateInlineModel(
        string sourceText,
        Markdig.Syntax.Inlines.Inline inline)
    {
        var kind = inline switch
        {
            LiteralInline => MarkdownInlineKind.Text,
            MathInline => MarkdownInlineKind.Math,
            FootnoteLink => MarkdownInlineKind.FootnoteLink,
            EmphasisInline emphasisStrike when emphasisStrike.DelimiterChar == '~' => MarkdownInlineKind.Strikethrough,
            EmphasisInline emphasisStrong when emphasisStrong.DelimiterCount >= 2 => MarkdownInlineKind.Strong,
            EmphasisInline => MarkdownInlineKind.Emphasis,
            CodeInline => MarkdownInlineKind.Code,
            LineBreakInline => MarkdownInlineKind.LineBreak,
            AutolinkInline => MarkdownInlineKind.Autolink,
            LinkInline linkNode when linkNode.IsImage => MarkdownInlineKind.Image,
            LinkInline => MarkdownInlineKind.Link,
            HtmlInline => MarkdownInlineKind.Html,
            Markdig.Extensions.TaskLists.TaskList => MarkdownInlineKind.Task,
            _ => MarkdownInlineKind.Unknown
        };

        var span = inline.Span;
        var start = Math.Clamp(span.Start, 0, sourceText.Length);
        var length = Math.Clamp(span.Length, 0, sourceText.Length - start);
        var source = sourceText.Substring(start, length);
        var text = inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            CodeInline code => code.Content,
            AutolinkInline autolinkNode => autolinkNode.Url,
            HtmlInline html => html.Tag,
            _ => null
        };
        var link = inline as LinkInline;
        var autolink = inline as AutolinkInline;
        var task = inline as Markdig.Extensions.TaskLists.TaskList;
        var delimiterChar = inline is EmphasisInline emphasis ? emphasis.DelimiterChar : '\0';
        var delimiterCount = inline is EmphasisInline emphasisNode ? emphasisNode.DelimiterCount : 0;
        var children = inline is ContainerInline container
            ? container.Select(child => CreateInlineModel(sourceText, child)).ToArray()
            : Array.Empty<MarkdownInlineModel>();

        return new MarkdownInlineModel(
            kind,
            source,
            start,
            length,
            text,
            link?.Url,
            link?.Title,
            autolink?.IsEmail ?? false,
            link?.IsImage ?? false,
            task?.Checked,
            delimiterChar,
            delimiterCount,
            new ReadOnlyCollection<MarkdownInlineModel>(children))
        {
            SyntaxType = inline.GetType().FullName ?? inline.GetType().Name,
            RequiresCompatibilityRenderer = kind is MarkdownInlineKind.Unknown
                or MarkdownInlineKind.Math
                or MarkdownInlineKind.FootnoteLink
        };
    }

    private static bool RequiresCompatibilityRenderer(MarkdownInlineModel inline)
        => inline.RequiresCompatibilityRenderer
            || inline.Children.Any(RequiresCompatibilityRenderer);
}
