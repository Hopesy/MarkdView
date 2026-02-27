using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Renderers;

public class MarkdownRendererTableTests
{
    [Fact]
    public void ConvertMarkdownToFlowDocument_ShouldRenderTableBlock_WhenMarkdownContainsTable()
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        var renderer = new MarkdownRenderer(pipeline);
        const string markdown = """
                                | Name | Score |
                                | ---- | -----:|
                                | Alice|  95   |
                                | Bob  |  88   |
                                """;

        var document = renderer.ConvertMarkdownToFlowDocument(
            markdown,
            new FontFamily("Segoe UI"),
            12,
            enableSyntaxHighlighting: false);

        var hasTable = false;
        foreach (var block in document.Blocks)
        {
            if (block is Table)
            {
                hasTable = true;
                break;
            }
        }

        Assert.True(hasTable);
    }
}
