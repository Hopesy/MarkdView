using System;
using System.Linq;
using System.Windows.Documents;
using Emoji.Wpf;
using MarkdView.Documents;
using MarkdView.Media;

namespace MarkdView.Renderers;

/// <summary>
/// 独立的 WPF 输出适配器。
/// 常见块节点、结构化表格、图片和代码块直接从稳定模型渲染；尚未迁移或存在特殊扩展语义的顶层块
/// 只回退到旧版兼容 renderer 处理自身源片段，以保证已迁移块不会被整篇文档回退污染。
/// </summary>
public sealed class WpfFlowDocumentRenderer : IMarkdownFlowDocumentRenderer
{
    private readonly MarkdownRenderer _legacyRenderer;
    private readonly WpfSimpleMarkdownRenderer _simpleRenderer;

    public WpfFlowDocumentRenderer(MarkdownRenderer legacyRenderer)
    {
        _legacyRenderer = legacyRenderer ?? throw new ArgumentNullException(nameof(legacyRenderer));
        _simpleRenderer = new WpfSimpleMarkdownRenderer(
            legacyRenderer.LinkNavigator,
            legacyRenderer.ImageLoaderAdapter,
            options => options.ImageLoadOptions
                ?? new MarkdownImageLoadOptions(
                    legacyRenderer.ImageLoadTimeout,
                    legacyRenderer.MaxImageBytes)
                {
                    MaxDecodePixel = legacyRenderer.MaxImageDecodePixel
                },
            options => options.MaxImagesPerDocument
                ?? legacyRenderer.MaxImagesPerDocument,
            legacyRenderer.CreateCodeBlockRenderer);
    }

    public FlowDocument ConvertDocumentToFlowDocument(
        MarkdownDocumentModel model,
        MarkdownRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);

        // 直接复用同一个适配器时，上一轮可能走过兼容 renderer；先取消其图片任务，
        // 防止旧文档在本轮纯模型渲染后仍被异步回写。
        _legacyRenderer.CancelPendingOperations();

        // 在进入模型/兼容双路径前冻结 renderer 的可变兼容属性，保证同一请求内策略一致。
        var effectiveOptions = FreezeOptions(options);

        if (_simpleRenderer.TryRender(model, effectiveOptions, out var document))
        {
            return document!;
        }

        // 保持一个 FlowDocument 和一套模型渲染会话，避免按块初始化图片计数/取消令牌。
        var codeBlockRenderer = _simpleRenderer.PrepareRender(effectiveOptions);
        var mixedDocument = _simpleRenderer.CreateFlowDocument(effectiveOptions);
        _legacyRenderer.BeginFragmentRenderSession(
            effectiveOptions,
            _simpleRenderer.ActiveRenderSession);
        foreach (var block in model.Blocks)
        {
            if (_simpleRenderer.CanRenderBlock(block, codeBlockRenderer))
            {
                mixedDocument.Blocks.Add(_simpleRenderer.RenderSupportedBlock(
                    block,
                    effectiveOptions.FontFamily,
                    effectiveOptions.FontSize,
                    codeBlockRenderer));
                continue;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[WpfFlowDocumentRenderer] Compatibility fallback for "
                + $"{(string.IsNullOrWhiteSpace(block.SyntaxType) ? block.Kind.ToString() : block.SyntaxType)} "
                + $"at source range {block.Start}:{block.Length}.");
            var fallbackDocument = _legacyRenderer.ConvertMarkdownFragmentToFlowDocument(
                block.SourceText,
                effectiveOptions);
            while (fallbackDocument.Blocks.Count > 0)
            {
                var fallbackBlock = fallbackDocument.Blocks.First();
                fallbackDocument.Blocks.Remove(fallbackBlock);
                mixedDocument.Blocks.Add(fallbackBlock);
            }
        }

        mixedDocument.SubstituteGlyphs();
        return mixedDocument;
    }

    private MarkdownRenderOptions FreezeOptions(MarkdownRenderOptions options)
    {
        var imageOptions = options.ImageLoadOptions
            ?? new MarkdownImageLoadOptions(
                _legacyRenderer.ImageLoadTimeout,
                _legacyRenderer.MaxImageBytes)
            {
                MaxDecodePixel = _legacyRenderer.MaxImageDecodePixel
            };
        var maxImages = options.MaxImagesPerDocument
            ?? _legacyRenderer.MaxImagesPerDocument;
        if (maxImages is < 0 or > MarkdownRenderDefaults.MaxImagesPerDocumentLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                maxImages,
                $"文档图片数量限制必须在 0 到 {MarkdownRenderDefaults.MaxImagesPerDocumentLimit} 之间。");
        }

        return new MarkdownRenderOptions(options.FontFamily, options.FontSize)
        {
            EnableSyntaxHighlighting = options.EnableSyntaxHighlighting,
            UseTransparentCanvas = options.UseTransparentCanvas,
            Foreground = options.Foreground,
            CodeBlockRenderer = options.CodeBlockRenderer,
            ImageLoadOptions = imageOptions,
            MaxImagesPerDocument = maxImages
        };
    }

    public void CancelPendingOperations()
    {
        _simpleRenderer.CancelPendingOperations();
        _legacyRenderer.CancelPendingOperations();
    }
}
