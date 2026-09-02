using MarkdView.Parsing;
using MarkdView.Renderers;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;

namespace MarkdView.Tests.Parsing;

public class MarkdigMarkdownParserTests
{
    [Fact]
    public void Parse_ShouldUseConfiguredMarkdigPipeline()
    {
        var parser = new MarkdigMarkdownParser(
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());

        var document = parser.Parse("# Title");

        var heading = Assert.IsType<HeadingBlock>(document[0]);
        var literal = Assert.IsType<LiteralInline>(heading.Inline!.FirstChild);
        Assert.Equal("Title", literal.Content.ToString());
    }

    [Fact]
    public void MarkdownRenderer_ShouldDelegateParsingToInjectedParser()
    {
        var pipeline = new MarkdownPipelineBuilder().Build();
        var parsed = Markdown.Parse("# Injected", pipeline);
        var parser = new RecordingParser(parsed);
        var renderer = new MarkdownRenderer(pipeline, parser: parser);

        var result = renderer.ParseMarkdown("ignored by fake");

        Assert.Same(parsed, result);
        Assert.Equal("ignored by fake", parser.Input);
    }

    private sealed class RecordingParser : IMarkdownParser
    {
        private readonly Markdig.Syntax.MarkdownDocument _document;

        public RecordingParser(Markdig.Syntax.MarkdownDocument document)
        {
            _document = document;
        }

        public string? Input { get; private set; }

        public Markdig.Syntax.MarkdownDocument Parse(string markdown)
        {
            Input = markdown;
            return _document;
        }
    }
}
