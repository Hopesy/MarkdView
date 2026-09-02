using System.Windows.Documents;
using MarkdView.Documents;

namespace MarkdView.Renderers;

/// <summary>
/// 将稳定文档模型适配为 WPF FlowDocument 的端口。
/// 实现必须在调用线程所属的 WPF Dispatcher 上创建 FlowDocument。
/// </summary>
public interface IMarkdownFlowDocumentRenderer
{
    FlowDocument ConvertDocumentToFlowDocument(
        MarkdownDocumentModel model,
        MarkdownRenderOptions options);

    void CancelPendingOperations();
}
