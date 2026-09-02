using System.Linq;
using System.Collections.Generic;
using Markdig;
using MarkdView.Documents;
using MarkdView.Parsing;
using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Documents;

public class MarkdownDocumentModelTests
{
    [Fact]
    public void PublicModelConstructor_ShouldSnapshotTopLevelBlocks()
    {
        var blocks = new List<MarkdownBlockModel>
        {
            new(
                MarkdownBlockKind.Paragraph,
                "text",
                0,
                4,
                0,
                null,
                false,
                1,
                System.Array.Empty<MarkdownBlockModel>())
        };

        var model = new MarkdownDocumentModel("text", blocks);
        blocks.Clear();

        Assert.Single(model.Blocks);
        Assert.Equal("text", model.SourceText);
        Assert.Equal(64, model.ContentHash.Length);
    }

    [Fact]
    public void ParseDocumentModel_ShouldExposeStableBlockMetadata()
    {
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());

        var model = renderer.ParseDocumentModel("# Title\n\n- item\n\n```csharp\nvar x = 1;\n```");

        Assert.Equal(64, model.ContentHash.Length);
        Assert.Equal("# Title\n\n- item\n\n```csharp\nvar x = 1;\n```", model.SourceText);
        Assert.Contains(model.Blocks, block => block.Kind == MarkdownBlockKind.Heading && block.HeadingLevel == 1);
        Assert.Contains(model.Blocks, block => block.Kind == MarkdownBlockKind.List && block.Children.Count > 0);
        Assert.Contains(model.Blocks.SelectMany(Flatten), block => block.Kind == MarkdownBlockKind.ListItem);
        Assert.Contains(model.Blocks, block => block.Kind == MarkdownBlockKind.Code && block.Language == "csharp");

        foreach (var block in model.Blocks.SelectMany(Flatten))
        {
            Assert.InRange(block.Start, 0, model.SourceText.Length);
            Assert.InRange(block.Length, 0, model.SourceText.Length - block.Start);
            Assert.Equal(block.SourceText, model.SourceText.Substring(block.Start, block.Length));
        }
    }

    [Fact]
    public void ContentHash_ShouldChangeWhenSourceChanges()
    {
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());

        var first = renderer.ParseDocumentModel("paragraph");
        var second = renderer.ParseDocumentModel("paragraph ");

        Assert.NotEqual(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void ParseDocumentModel_ShouldPreserveInlineSemanticsAndSourceRanges()
    {
        var renderer = new MarkdownRenderer(
            new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTaskLists().Build());

        const string markdown = "**bold** and [docs](https://example.com) `code`\n\n- [x] done";
        var model = renderer.ParseDocumentModel(markdown);
        var paragraph = Assert.IsType<MarkdownBlockModel>(model.Blocks[0]);

        Assert.Collection(
            paragraph.Inlines,
            inline => Assert.Equal(MarkdownInlineKind.Strong, inline.Kind),
            inline => Assert.Equal(MarkdownInlineKind.Text, inline.Kind),
            inline =>
            {
                Assert.Equal(MarkdownInlineKind.Link, inline.Kind);
                Assert.Equal("https://example.com", inline.Url);
                Assert.Single(inline.Children);
            },
            inline => Assert.Equal(MarkdownInlineKind.Text, inline.Kind),
            inline =>
            {
                Assert.Equal(MarkdownInlineKind.Code, inline.Kind);
                Assert.Equal("code", inline.Text);
            });

        var list = Assert.Single(model.Blocks, block => block.Kind == MarkdownBlockKind.List);
        var taskParagraph = Assert.Single(list.Children).Children[0];
        var task = Assert.Single(taskParagraph.Inlines, inline => inline.Kind == MarkdownInlineKind.Task);
        Assert.True(task.IsChecked);

        foreach (var inline in paragraph.Inlines.SelectMany(Flatten))
        {
            Assert.Equal(inline.SourceText, markdown.Substring(inline.Start, inline.Length));
        }
    }

    [Fact]
    public void ParseDocumentModel_ShouldPreserveTableColumnsAndAlignment()
    {
        var renderer = new MarkdownRenderer(
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        var model = renderer.ParseDocumentModel(
            "| Name | Score |\n| :--- | ---: |\n| Alice | 95 |");

        var table = Assert.Single(model.Blocks, block => block.Kind == MarkdownBlockKind.Table);
        Assert.Equal(MarkdownTableColumnAlignment.Left, table.TableColumnAlignments[0]);
        Assert.Equal(MarkdownTableColumnAlignment.Right, table.TableColumnAlignments[1]);

        var row = Assert.IsType<MarkdownBlockModel>(table.Children.First(child =>
            child.Kind == MarkdownBlockKind.TableRow && child.IsTableHeader));
        var cells = row.Children.Where(child => child.Kind == MarkdownBlockKind.TableCell).ToArray();
        Assert.Equal(new[] { 0, 1 }, cells.Select(cell => cell.ColumnIndex));
        Assert.All(cells, cell => Assert.Equal(1, cell.ColumnSpan));
        Assert.All(cells, cell => Assert.Equal(1, cell.RowSpan));
    }

    [Fact]
    public void ParseDocumentModel_ShouldClassifyComplexExtensionsForFutureMigration()
    {
        var renderer = new MarkdownRenderer(MarkdownPipelineFactory.CreateDefault());

        var definition = renderer.ParseDocumentModel("Term\n:   definition");
        Assert.Contains(
            definition.Blocks.SelectMany(Flatten),
            block => block.Kind is MarkdownBlockKind.DefinitionList
                or MarkdownBlockKind.DefinitionItem
                or MarkdownBlockKind.DefinitionTerm);
        Assert.Contains(
            definition.Blocks.SelectMany(Flatten),
            block => block.RequiresCompatibilityRenderer && !string.IsNullOrWhiteSpace(block.SyntaxType));

        var footnote = renderer.ParseDocumentModel("note[^1]\n\n[^1]: detail");
        Assert.Contains(
            footnote.Blocks.SelectMany(Flatten),
            block => block.Kind is MarkdownBlockKind.FootnoteGroup or MarkdownBlockKind.Footnote);
        Assert.Contains(
            footnote.Blocks
                .SelectMany(Flatten)
                .SelectMany(block => block.Inlines.SelectMany(Flatten)),
            inline => inline.Kind == MarkdownInlineKind.FootnoteLink
                && inline.RequiresCompatibilityRenderer);

        var math = renderer.ParseDocumentModel("$x$\n\n$$\ny = x\n$$");
        Assert.Contains(
            math.Blocks.SelectMany(Flatten),
            block => block.Kind == MarkdownBlockKind.Math && block.RequiresCompatibilityRenderer);
        Assert.Contains(
            math.Blocks
                .SelectMany(Flatten)
                .SelectMany(block => block.Inlines.SelectMany(Flatten)),
            inline => inline.Kind == MarkdownInlineKind.Math && inline.RequiresCompatibilityRenderer);
    }

    private static System.Collections.Generic.IEnumerable<MarkdownBlockModel> Flatten(MarkdownBlockModel block)
    {
        yield return block;
        foreach (var child in block.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }

    private static System.Collections.Generic.IEnumerable<MarkdownInlineModel> Flatten(MarkdownInlineModel inline)
    {
        yield return inline;
        foreach (var child in inline.Children.SelectMany(Flatten))
        {
            yield return child;
        }
    }
}
