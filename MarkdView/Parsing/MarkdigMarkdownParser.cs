using System;
using Markdig;
using Markdig.Syntax;

namespace MarkdView.Parsing;

/// <summary>
/// 基于 Markdig 的默认解析器适配器。
/// </summary>
public sealed class MarkdigMarkdownParser : IMarkdownParser
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdigMarkdownParser(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    public MarkdownDocument Parse(string markdown)
        => Markdown.Parse(markdown ?? string.Empty, _pipeline);
}
