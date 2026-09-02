using Markdig.Syntax;

namespace MarkdView.Parsing;

/// <summary>
/// Markdown 解析端口。解析阶段不创建 WPF 对象，也不执行网络或系统副作用。
/// </summary>
public interface IMarkdownParser
{
    MarkdownDocument Parse(string markdown);
}
