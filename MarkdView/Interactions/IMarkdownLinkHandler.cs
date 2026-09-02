using System;

namespace MarkdView.Interactions;

/// <summary>
/// 外部链接处理端口。实现方可以替换默认 Shell 行为，加入审计、拦截或宿主内导航。
/// </summary>
public interface IMarkdownLinkHandler
{
    void Open(Uri uri);
}
