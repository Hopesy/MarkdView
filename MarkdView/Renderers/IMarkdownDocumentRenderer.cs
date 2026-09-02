using System.Windows.Documents;
using MarkdView.Documents;
using MarkdView.Parsing;

namespace MarkdView.Renderers;

/// <summary>
/// 解析端口和 WPF 输出端口的组合兼容接口。
/// 新代码应优先分别依赖 <see cref="MarkdView.Parsing.IMarkdownDocumentParser"/> 与
/// <see cref="IMarkdownFlowDocumentRenderer"/>，该接口用于保持现有默认 renderer 的兼容构造路径。
/// </summary>
public interface IMarkdownDocumentRenderer : IMarkdownDocumentParser, IMarkdownFlowDocumentRenderer
{
}
