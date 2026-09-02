using System;
using MarkdView.Media;

namespace MarkdView.Renderers;

/// <summary>
/// 渲染请求的安全和策略默认值。
/// 兼容 renderer、模型 renderer 和控件默认依赖同一组值，避免不同入口产生不同安全边界。
/// </summary>
internal static class MarkdownRenderDefaults
{
    public static readonly TimeSpan ImageLoadTimeout = TimeSpan.FromSeconds(10);
    public const long MaxImageBytes = 8 * 1024 * 1024;
    public const int MaxImagesPerDocument = 64;
    public const int MaxImagesPerDocumentLimit = 4096;
    public const int MaxImageDecodePixel = MarkdownImageLoadOptions.DefaultMaxDecodePixel;

    public static MarkdownImageLoadOptions CreateImageLoadOptions(int maxDecodePixel = MaxImageDecodePixel)
        => new(ImageLoadTimeout, MaxImageBytes)
        {
            MaxDecodePixel = maxDecodePixel
        };
}
