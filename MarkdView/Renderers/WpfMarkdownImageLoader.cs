using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarkdView.Media;

namespace MarkdView.Renderers;

/// <summary>
/// 将图片加载端口的异步结果安装到 WPF 图片占位元素。
/// 下载和解码由 <see cref="IMarkdownImageLoader"/> 完成，本类只负责 WPF 生命周期和失败状态呈现。
/// </summary>
internal sealed class WpfMarkdownImageLoader
{
    private readonly IMarkdownImageLoader _loader;

    public WpfMarkdownImageLoader(IMarkdownImageLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public async Task LoadIntoAsync(
        Border border,
        Uri uri,
        TextBlock placeholder,
        MarkdownImageLoadOptions options,
        Thickness imageMargin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(placeholder);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var bitmap = await _loader
                .LoadAsync(uri, options, cancellationToken)
                .ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            border.Child = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                MaxWidth = Application.Current?.TryFindResource("Markdown.Image.MaxWidth") is double maxWidth
                    && maxWidth > 0
                    ? maxWidth
                    : MarkdownLayoutDefaults.ImageMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = imageMargin
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 文档已被替换，旧图片任务无需更新已经脱离视觉树的占位符。
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // 错误提示只显示 host/path，避免把查询串中的签名或访问令牌写入界面。
            var safeDisplayUrl = uri.GetLeftPart(UriPartial.Path);
            var displayUrl = safeDisplayUrl.Length > 256
                ? safeDisplayUrl[..256] + "..."
                : safeDisplayUrl;
            placeholder.Text = $"[图片加载失败: {displayUrl}]";
            placeholder.Foreground = Application.Current?.TryFindResource("Markdown.Error.Foreground") is Brush errorBrush
                ? errorBrush
                : Brushes.Red;
            System.Diagnostics.Debug.WriteLine($"[WpfMarkdownImageLoader] 图片加载失败: {ex.Message}");
        }
    }
}
