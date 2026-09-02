using System;
using MarkdView.Documents;

namespace MarkdView.Parsing;

/// <summary>
/// 将现有 Markdig 解析器适配为稳定文档模型。
/// </summary>
public sealed class MarkdigMarkdownDocumentParser : IMarkdownDocumentParser
{
    private readonly IMarkdownParser _parser;

    public MarkdigMarkdownDocumentParser(IMarkdownParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public MarkdownDocumentModel Parse(string markdown)
    {
        var source = markdown ?? string.Empty;
        return MarkdigMarkdownDocumentModelFactory.Create(source, _parser.Parse(source));
    }
}
