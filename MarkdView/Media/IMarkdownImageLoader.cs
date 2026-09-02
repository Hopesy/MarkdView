using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MarkdView.Media;

/// <summary>
/// Markdown 图片加载端口。默认实现负责 HTTP，宿主可以替换为缓存、代理或离线实现。
/// </summary>
public interface IMarkdownImageLoader
{
    Task<BitmapSource> LoadAsync(Uri uri, MarkdownImageLoadOptions options, CancellationToken cancellationToken);
}
