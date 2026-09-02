using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using Markdig;
using MarkdView.Renderers;
using MarkdView;
using MarkdView.Enums;
using System.Windows;
using Xunit;

namespace MarkdView.Tests.Renderers;

public class MarkdownRendererTableTests
{
    [Fact]
    public void ConvertMarkdownToFlowDocument_ShouldRenderTableBlock()
    {
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        const string markdown = "| Name | Score |\n| ---- | -----:|\n| Alice|  95   |\n| Bob  |  88   |";
        var document = renderer.ConvertMarkdownToFlowDocument(markdown, new FontFamily("Segoe UI"), 12, false);
        Assert.Contains(document.Blocks, block => block is Table);
    }

    [Fact]
    public void ConvertMarkdownToFlowDocument_ShouldResolveValuesFromMergedThemeDictionary()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);

            var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var document = renderer.ConvertMarkdownToFlowDocument(
                "# Title\n\n---",
                new FontFamily("Segoe UI"),
                12,
                false);

            Assert.Equal(new Thickness(12, 6, 12, 8), document.PagePadding);
            var heading = Assert.IsType<Paragraph>(document.Blocks.First());
            Assert.Equal(Color.FromRgb(0x0F, 0x17, 0x2A), Assert.IsType<SolidColorBrush>(heading.Foreground).Color);
        });
    }

    [Fact]
    public void ConvertMarkdownToFlowDocument_ShouldUseCompactBlockSpacing()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);

            var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var document = renderer.ConvertMarkdownToFlowDocument(
                "# Title\n\nParagraph\n\n```csharp\nvar value = 1;\n```",
                new FontFamily("Segoe UI"),
                12,
                false);

            var heading = Assert.IsType<Paragraph>(document.Blocks.First());
            Assert.Equal(new Thickness(0, 10, 0, 6), heading.Margin);

            var paragraph = Assert.IsType<Paragraph>(document.Blocks.Skip(1).First());
            Assert.Equal(new Thickness(0, 1, 0, 4), paragraph.Margin);

            var codeBlock = Assert.IsType<BlockUIContainer>(document.Blocks.Skip(2).First());
            var codeContainer = Assert.IsType<Grid>(codeBlock.Child);
            Assert.Equal(new Thickness(0, 4, 0, 6), codeContainer.Margin);

            var mainBorder = Assert.IsType<Border>(codeContainer.Children[0]);
            var containerGrid = Assert.IsType<Grid>(mainBorder.Child);
            Assert.Equal(28, containerGrid.RowDefinitions[0].Height.Value);

            var codeScrollViewer = Assert.IsType<ScrollViewer>(containerGrid.Children[1]);
            Assert.Equal(new Thickness(8, 4, 8, 4), codeScrollViewer.Padding);
        });
    }

    [Fact]
    public void RenderCommonInlineFeatures_ShouldPreserveMeaning()
    {
        RunInSta(() =>
        {
            var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTaskLists().Build());
            var document = renderer.ConvertMarkdownToFlowDocument(
                "~~deleted~~ and <https://example.com>\n\n- [ ] todo\n- [x] done",
                new FontFamily("Segoe UI"), 12, false);
            var paragraph = Assert.IsType<Paragraph>(document.Blocks.First());
            Assert.Contains(paragraph.Inlines, inline => inline is Span span && span.TextDecorations != null);
            Assert.Contains(paragraph.Inlines, inline => inline is Hyperlink link && link.NavigateUri?.Host == "example.com");
            Assert.Contains(document.Blocks, block => block is List list && list.ListItems.Count == 2);
        });
    }

    [Fact]
    public void OrderedList_ShouldPreserveMarkdownStartIndex()
    {
        RunInSta(() =>
        {
            var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var document = renderer.ConvertMarkdownToFlowDocument(
                "3. third\n4. fourth",
                new FontFamily("Segoe UI"),
                12,
                false);

            var list = Assert.IsType<List>(document.Blocks.First());
            Assert.True(list.MarkerStyle == TextMarkerStyle.Decimal);
            Assert.Equal(3, list.StartIndex);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }
}
