namespace MarkdView.Interactions;

/// <summary>
/// 剪贴板端口。渲染器通过此接口复制代码，避免直接绑定 WPF Clipboard。
/// </summary>
public interface IClipboardService
{
    void SetText(string text);
}
