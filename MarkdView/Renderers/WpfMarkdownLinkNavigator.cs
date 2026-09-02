using System;
using System.Collections.Generic;
using System.Windows.Navigation;
using MarkdView.Interactions;

namespace MarkdView.Renderers;

/// <summary>
/// WPF Hyperlink 的导航适配器。外部进程启动通过 <see cref="IMarkdownLinkHandler"/> 注入，
/// 本类只处理 WPF 事件完成状态和异常隔离。
/// </summary>
internal sealed class WpfMarkdownLinkNavigator
{
    private static readonly HashSet<string> SafeExternalSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeMailto
    };

    private readonly IMarkdownLinkHandler _linkHandler;

    public WpfMarkdownLinkNavigator(IMarkdownLinkHandler linkHandler)
    {
        _linkHandler = linkHandler ?? throw new ArgumentNullException(nameof(linkHandler));
    }

    public void Handle(RequestNavigateEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Uri == null || !IsAllowedScheme(e.Uri.Scheme))
        {
            e.Handled = true;
            return;
        }

        try
        {
            _linkHandler.Open(e.Uri);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WpfMarkdownLinkNavigator] 打开链接失败: {ex.Message}");
        }
        finally
        {
            e.Handled = true;
        }
    }

    public static bool IsAllowedScheme(string? scheme)
        => !string.IsNullOrWhiteSpace(scheme) && SafeExternalSchemes.Contains(scheme);
}
