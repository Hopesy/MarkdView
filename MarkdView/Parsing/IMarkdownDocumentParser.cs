using MarkdView.Documents;

namespace MarkdView.Parsing;

/// <summary>
/// Markdown 到稳定文档模型的解析端口。
/// </summary>
public interface IMarkdownDocumentParser
{
    MarkdownDocumentModel Parse(string markdown);
}
