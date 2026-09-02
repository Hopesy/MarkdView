using System;
using System.Windows;
using System.Windows.Media;
using MarkdView.Media;

namespace MarkdView.Renderers;

/// <summary>
/// 一次 Markdown 渲染所使用的不可变配置快照。
/// 通过单个对象传递配置，避免渲染入口继续增加位置参数。
/// </summary>
public sealed class MarkdownRenderOptions
{
    private int? _maxImagesPerDocument;

    public MarkdownRenderOptions(FontFamily fontFamily, double fontSize = 14.0)
    {
        FontFamily = fontFamily ?? throw new ArgumentNullException(nameof(fontFamily));
        FontSize = fontSize;
        ValidateFontSize(fontSize);
    }

    public FontFamily FontFamily { get; }

    public double FontSize { get; }

    public bool EnableSyntaxHighlighting { get; init; } = true;

    public bool UseTransparentCanvas { get; init; }

    public Brush? Foreground { get; init; }

    public CodeBlockRenderer? CodeBlockRenderer { get; init; }

    /// <summary>
    /// 当前渲染请求使用的图片加载限制，包括超时、响应大小和解码像素上限。
    /// 为空时由 renderer 的兼容属性提供默认值。
    /// </summary>
    public MarkdownImageLoadOptions? ImageLoadOptions { get; init; }

    /// <summary>
    /// 当前文档允许加载的最大图片数量，范围为 0 到 4096。为空时由 renderer 的兼容属性提供默认值。
    /// </summary>
    public int? MaxImagesPerDocument
    {
        get => _maxImagesPerDocument;
        init
        {
            if (value is < 0 or > MarkdownRenderDefaults.MaxImagesPerDocumentLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxImagesPerDocument),
                    value,
                    $"文档图片数量限制必须在 0 到 {MarkdownRenderDefaults.MaxImagesPerDocumentLimit} 之间。");
            }

            _maxImagesPerDocument = value;
        }
    }

    private static void ValidateFontSize(double fontSize)
    {
        if (double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize <= 0 || fontSize > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "字号必须大于 0 且不超过 200。");
        }
    }
}
