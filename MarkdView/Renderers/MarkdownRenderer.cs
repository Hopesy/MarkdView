using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Emoji.Wpf;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using MarkdigTableColumnAlign = Markdig.Extensions.Tables.TableColumnAlign;
using MarkdView.Interactions;
using MarkdView.Media;
using MarkdView.Parsing;
using MarkdView.Services;
using MarkdView.Documents;

namespace MarkdView.Renderers;

/// <summary>
/// Markdown 渲染器 - 负责将 Markdown 文本转换为 WPF FlowDocument
/// </summary>
public class MarkdownRenderer : IMarkdownDocumentRenderer
{
    private readonly object _renderLock = new();
    private readonly IMarkdownParser _parser;
    private readonly IMarkdownDocumentParser _documentParser;
    private readonly IClipboardService? _clipboardService;
    private readonly ISyntaxHighlighter? _syntaxHighlighter;
    private double _baseFontSize;
    private FontFamily _fontFamily;
    private MarkdownRenderSession? _activeRenderSession;
    private readonly IMarkdownImageLoader _imageLoader;
    private readonly WpfMarkdownImageLoader _wpfImageLoader;
    private readonly WpfMarkdownLinkNavigator _linkNavigator;

    /// <summary>
    /// 保留 v1.0.10 及更早版本的单参数构造函数签名。
    /// 可选参数不会生成旧的 CLR 构造函数元数据，因此不能只依赖下面的扩展构造函数。
    /// </summary>
    public MarkdownRenderer(MarkdownPipeline pipeline)
        : this(pipeline, null, null, null, null, null)
    {
    }

    public MarkdownRenderer(
        MarkdownPipeline pipeline,
        IMarkdownImageLoader? imageLoader = null,
        IMarkdownLinkHandler? linkHandler = null,
        IMarkdownParser? parser = null,
        IClipboardService? clipboardService = null,
        ISyntaxHighlighter? syntaxHighlighter = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _parser = parser ?? new MarkdigMarkdownParser(pipeline);
        _documentParser = new MarkdigMarkdownDocumentParser(_parser);
        _clipboardService = clipboardService;
        _syntaxHighlighter = syntaxHighlighter;
        _imageLoader = imageLoader ?? new HttpMarkdownImageLoader();
        _wpfImageLoader = new WpfMarkdownImageLoader(_imageLoader);
        _linkNavigator = new WpfMarkdownLinkNavigator(linkHandler ?? new ShellMarkdownLinkHandler());
        _baseFontSize = 12.0;
        _fontFamily = new FontFamily("Noto Sans, SF Pro SC, SF Pro Text, SF Pro Icons, PingFang SC, Helvetica Neue, Helvetica, Arial");
        ImageLoadTimeout = MarkdownRenderDefaults.ImageLoadTimeout;
        MaxImageBytes = MarkdownRenderDefaults.MaxImageBytes;
        MaxImagesPerDocument = MarkdownRenderDefaults.MaxImagesPerDocument;
        MaxImageDecodePixel = MarkdownRenderDefaults.MaxImageDecodePixel;
        _activeImageLoadOptions = MarkdownRenderDefaults.CreateImageLoadOptions(MaxImageDecodePixel);
        _activeMaxImagesPerDocument = MaxImagesPerDocument;
    }

    private TimeSpan _imageLoadTimeout;
    private long _maxImageBytes;
    private int _maxImagesPerDocument;
    private MarkdownImageLoadOptions _activeImageLoadOptions;
    private int _activeMaxImagesPerDocument;

    /// <summary>
    /// 默认单张图片加载超时，范围为大于 0 且不超过 10 分钟；请求快照可覆盖此值。
    /// </summary>
    public TimeSpan ImageLoadTimeout
    {
        get => _imageLoadTimeout;
        set => _imageLoadTimeout = value > TimeSpan.Zero && value <= MarkdownImageLoadOptions.MaxTimeout
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "图片加载超时必须大于 0 且不超过 10 分钟。");
    }

    /// <summary>
    /// 默认单张图片响应大小上限，范围为 1 到 256 MB；请求快照可覆盖此值。
    /// </summary>
    public long MaxImageBytes
    {
        get => _maxImageBytes;
        set => _maxImageBytes = value > 0 && value <= MarkdownImageLoadOptions.MaxAllowedBytes
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "图片大小限制必须在 1 到 256 MB 之间。");
    }

    /// <summary>
    /// 默认单个文档图片数量上限，范围为 0 到 4096；0 表示阻止图片加载。
    /// </summary>
    public int MaxImagesPerDocument
    {
        get => _maxImagesPerDocument;
        set => _maxImagesPerDocument = value >= 0 && value <= MarkdownRenderDefaults.MaxImagesPerDocumentLimit
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "文档图片数量限制必须在 0 到 4096 之间。");
    }

    /// <summary>
    /// 图片解码时允许的最大宽/高像素。较小的值可以降低超大图片的内存占用。
    /// </summary>
    public int MaxImageDecodePixel
    {
        get => _maxImageDecodePixel;
        set => _maxImageDecodePixel = value is > 0 and <= 8192
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "图片解码尺寸必须在 1 到 8192 像素之间。");
    }

    private int _maxImageDecodePixel;

    /// <summary>
    /// 取消当前文档仍在等待的图片加载和其他 renderer 级异步副作用。
    /// </summary>
    public void CancelPendingOperations()
    {
        lock (_renderLock)
        {
            _activeRenderSession?.Cancel();
        }
    }

    // 保留内部别名，兼容协调器迁移前的调用点。
    internal void CancelPendingImageLoads() => CancelPendingOperations();

    /// <summary>
    /// 保留旧版六参数方法签名。新能力通过下面的七参数 overload 或 options 入口提供。
    /// </summary>
    public FlowDocument ConvertMarkdownToFlowDocument(
        string markdown,
        FontFamily fontFamily,
        double fontSize,
        bool enableSyntaxHighlighting,
        CodeBlockRenderer? codeBlockRenderer = null,
        bool useTransparentCanvas = false)
        => ConvertMarkdownToFlowDocument(
            markdown,
            fontFamily,
            fontSize,
            enableSyntaxHighlighting,
            codeBlockRenderer,
            useTransparentCanvas,
            foregroundOverride: null);

    /// <summary>
    /// 使用扩展的兼容入口渲染 Markdown。
    /// </summary>
    public FlowDocument ConvertMarkdownToFlowDocument(
        string markdown,
        FontFamily fontFamily,
        double fontSize,
        bool enableSyntaxHighlighting,
        CodeBlockRenderer? codeBlockRenderer,
        bool useTransparentCanvas,
        Brush? foregroundOverride)
    {
        return ConvertMarkdownToFlowDocument(markdown, new MarkdownRenderOptions(fontFamily, fontSize)
        {
            EnableSyntaxHighlighting = enableSyntaxHighlighting,
            CodeBlockRenderer = codeBlockRenderer,
            UseTransparentCanvas = useTransparentCanvas,
            Foreground = foregroundOverride
        });
    }

    /// <summary>
    /// 使用一次性的配置快照渲染 Markdown。新功能应优先使用此入口。
    /// </summary>
    public FlowDocument ConvertMarkdownToFlowDocument(string markdown, MarkdownRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var document = ParseMarkdown(markdown);
        return ConvertDocumentToFlowDocument(document, options);
    }

    /// <summary>
    /// 将已经解析的 AST 转换为 FlowDocument。解析可在后台线程完成，WPF 对象必须在 UI 线程创建。
    /// </summary>
    public FlowDocument ConvertDocumentToFlowDocument(
        MarkdownDocument document,
        FontFamily fontFamily,
        double fontSize,
        bool enableSyntaxHighlighting,
        CodeBlockRenderer? codeBlockRenderer = null,
        bool useTransparentCanvas = false,
        Brush? foregroundOverride = null)
    {
        return ConvertDocumentToFlowDocument(document, new MarkdownRenderOptions(fontFamily, fontSize)
        {
            EnableSyntaxHighlighting = enableSyntaxHighlighting,
            CodeBlockRenderer = codeBlockRenderer,
            UseTransparentCanvas = useTransparentCanvas,
            Foreground = foregroundOverride
        });
    }

    /// <summary>
    /// 将 AST 转换为 WPF FlowDocument。AST 可由后台线程预先生成，当前方法必须在 UI 线程调用。
    /// </summary>
    public FlowDocument ConvertDocumentToFlowDocument(MarkdownDocument document, MarkdownRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        lock (_renderLock)
        {
            return ConvertDocumentToFlowDocumentCore(document, options);
        }
    }

    private FlowDocument ConvertDocumentToFlowDocumentCore(MarkdownDocument document, MarkdownRenderOptions options)
        => ConvertDocumentToFlowDocumentCore(document, options, resetAsyncState: true);

    private FlowDocument ConvertDocumentToFlowDocumentCore(
        MarkdownDocument document,
        MarkdownRenderOptions options,
        bool resetAsyncState)
    {

        _baseFontSize = options.FontSize;
        _fontFamily = options.FontFamily;
        _activeImageLoadOptions = options.ImageLoadOptions
            ?? new MarkdownImageLoadOptions(ImageLoadTimeout, MaxImageBytes)
            {
                MaxDecodePixel = MaxImageDecodePixel
            };
        _activeMaxImagesPerDocument = options.MaxImagesPerDocument
            ?? MaxImagesPerDocument;
        if (_activeMaxImagesPerDocument is < 0 or > MarkdownRenderDefaults.MaxImagesPerDocumentLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _activeMaxImagesPerDocument,
                $"文档图片数量限制必须在 0 到 {MarkdownRenderDefaults.MaxImagesPerDocumentLimit} 之间。");
        }
        if (resetAsyncState)
        {
            ResetAsyncRenderState();
        }

        // 创建 FlowDocument 并应用基础样式
        var flowDocument = new FlowDocument
        {
            FontFamily = options.FontFamily,
            FontSize = options.FontSize,
            LineHeight = GetDouble("Markdown.LineHeight", MarkdownLayoutDefaults.LineHeightScale) * options.FontSize,
            PagePadding = GetThickness("Markdown.PagePadding", MarkdownLayoutDefaults.PagePadding)
        };

        // 用户显式设置的 Foreground 优先，否则跟随主题资源。
        if (options.Foreground != null)
        {
            flowDocument.Foreground = options.Foreground;
        }
        else
        {
            SetDynamicResource(flowDocument, FlowDocument.ForegroundProperty, "Markdown.Foreground",
                new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));
        }
        if (options.UseTransparentCanvas)
        {
            flowDocument.Background = Brushes.Transparent;
        }
        else
        {
            SetDynamicResource(flowDocument, FlowDocument.BackgroundProperty, "Markdown.Background",
                new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)));
        }

        var effectiveCodeBlockRenderer = CreateCodeBlockRenderer(options);

        // 遍历所有块级元素
        foreach (var block in document)
        {
            var element = ConvertBlock(block, options.EnableSyntaxHighlighting, effectiveCodeBlockRenderer);
            if (element != null)
            {
                flowDocument.Blocks.Add(element);
            }
        }

        // 使用 Emoji.Wpf 替换 emoji 字符为彩色 emoji
        flowDocument.SubstituteGlyphs();

        return flowDocument;
    }

    /// <summary>
    /// 开始一次由多个 Markdown 片段组成的兼容渲染会话。
    /// 混合模型 renderer 使用它来保证不同回退块共享图片数量限制和取消令牌。
    /// </summary>
    internal void BeginFragmentRenderSession(
        MarkdownRenderOptions options,
        MarkdownRenderSession? sharedSession = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_renderLock)
        {
            _baseFontSize = options.FontSize;
            _fontFamily = options.FontFamily;
            _activeImageLoadOptions = options.ImageLoadOptions
                ?? new MarkdownImageLoadOptions(ImageLoadTimeout, MaxImageBytes)
                {
                    MaxDecodePixel = MaxImageDecodePixel
                };
            _activeMaxImagesPerDocument = options.MaxImagesPerDocument
                ?? MaxImagesPerDocument;
            if (_activeMaxImagesPerDocument is < 0 or > MarkdownRenderDefaults.MaxImagesPerDocumentLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    _activeMaxImagesPerDocument,
                    $"文档图片数量限制必须在 0 到 {MarkdownRenderDefaults.MaxImagesPerDocumentLimit} 之间。");
            }
            ResetAsyncRenderState(sharedSession);
        }
    }

    /// <summary>
    /// 在当前兼容渲染会话中转换一个 Markdown 片段，不会取消之前片段的异步图片加载。
    /// 调用方必须先调用 <see cref="BeginFragmentRenderSession"/>。
    /// </summary>
    internal FlowDocument ConvertMarkdownFragmentToFlowDocument(
        string markdown,
        MarkdownRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var document = ParseMarkdown(markdown);
        lock (_renderLock)
        {
            return ConvertDocumentToFlowDocumentCore(document, options, resetAsyncState: false);
        }
    }

    private void ResetAsyncRenderState(MarkdownRenderSession? sharedSession = null)
    {
        if (_activeRenderSession != null && !ReferenceEquals(_activeRenderSession, sharedSession))
        {
            _activeRenderSession.Dispose();
        }

        _activeRenderSession = sharedSession ?? new MarkdownRenderSession();
    }

    /// <summary>
    /// 仅解析 Markdown，不创建任何 WPF 对象，可从后台线程调用。
    /// </summary>
    public MarkdownDocument ParseMarkdown(string markdown)
    {
        return _parser.Parse(markdown ?? string.Empty);
    }

    /// <summary>
    /// 生成不暴露 Markdig 类型的稳定文档快照，供缓存、增量渲染和非 WPF 输出使用。
    /// </summary>
    public MarkdownDocumentModel ParseDocumentModel(string markdown)
        => _documentParser.Parse(markdown ?? string.Empty);

    /// <summary>
    /// 模型解析端口的标准入口；保留 ParseDocumentModel 作为更明确的兼容别名。
    /// </summary>
    public MarkdownDocumentModel Parse(string markdown)
        => ParseDocumentModel(markdown);

    /// <summary>
    /// 从稳定文档模型渲染。兼容 renderer 只依赖快照原文重新解析，模型本身不携带 Markdig AST。
    /// </summary>
    public FlowDocument ConvertDocumentToFlowDocument(
        MarkdownDocumentModel model,
        MarkdownRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        return ConvertMarkdownToFlowDocument(model.SourceText, options);
    }

    private static void ValidateFontSize(double fontSize)
    {
        if (double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize <= 0 || fontSize > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), fontSize, "字号必须大于 0 且不超过 200。");
        }
    }

    #region Block 转换

    /// <summary>
    /// 将 Markdig Block 转换为 WPF Block
    /// </summary>
    private WpfBlock? ConvertBlock(MarkdigBlock block, bool enableSyntaxHighlighting, CodeBlockRenderer codeBlockRenderer, int listLevel = 0)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading),
            ParagraphBlock paragraph => ConvertParagraph(paragraph),
            QuoteBlock quote => ConvertQuote(quote, enableSyntaxHighlighting, codeBlockRenderer),
            ListBlock list => ConvertList(list, enableSyntaxHighlighting, codeBlockRenderer, listLevel),
            MarkdigTable table => ConvertTable(table, enableSyntaxHighlighting, codeBlockRenderer),
            MathBlock math => ConvertMathBlock(math),
            DefinitionList definitionList => ConvertExtensionContainer(definitionList, enableSyntaxHighlighting, codeBlockRenderer),
            DefinitionItem definitionItem => ConvertExtensionContainer(definitionItem, enableSyntaxHighlighting, codeBlockRenderer),
            FootnoteGroup footnoteGroup => ConvertExtensionContainer(footnoteGroup, enableSyntaxHighlighting, codeBlockRenderer),
            Footnote footnote => ConvertExtensionContainer(footnote, enableSyntaxHighlighting, codeBlockRenderer),
            DefinitionTerm definitionTerm => ConvertExtensionLeaf(definitionTerm),
            CodeBlock code => ConvertCodeBlock(code, codeBlockRenderer),
            ThematicBreakBlock => ConvertThematicBreak(),
            HtmlBlock htmlBlock => ConvertHtmlBlock(htmlBlock),
            _ => ConvertUnknownBlock(block)
        };
    }

    private WpfBlock ConvertHtmlBlock(HtmlBlock htmlBlock)
    {
        var text = htmlBlock.Lines.ToString();
        return new Paragraph(new Run(text))
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin),
            FontFamily = _fontFamily
        };
    }

    private WpfBlock ConvertUnknownBlock(MarkdigBlock block)
    {
        System.Diagnostics.Debug.WriteLine($"[MarkdownRenderer] Unsupported block {block.GetType().FullName}; rendered as text.");
        return new Paragraph(new Run(block.ToString() ?? string.Empty))
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin)
        };
    }

    private WpfBlock ConvertMathBlock(MathBlock math)
    {
        return new Paragraph(new Run(math.Lines.ToString()))
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin),
            FontFamily = _fontFamily
        };
    }

    private WpfBlock ConvertExtensionContainer(
        ContainerBlock container,
        bool enableSyntaxHighlighting,
        CodeBlockRenderer codeBlockRenderer)
    {
        var section = new Section
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin)
        };

        foreach (var child in container)
        {
            var rendered = ConvertBlock(child, enableSyntaxHighlighting, codeBlockRenderer);
            if (rendered != null)
            {
                section.Blocks.Add(rendered);
            }
        }

        if (section.Blocks.Count == 0)
        {
            section.Blocks.Add(new Paragraph(new Run(container.ToString() ?? string.Empty))
            {
                Margin = new Thickness(0)
            });
        }

        return section;
    }

    private WpfBlock ConvertExtensionLeaf(DefinitionTerm term)
        => new Paragraph(new Run(term.ToString() ?? string.Empty))
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin),
            FontFamily = _fontFamily
        };

    /// <summary>
    /// 转换标题块
    /// </summary>
    private WpfBlock ConvertHeading(HeadingBlock heading)
    {
        var level = heading.Level;
        var paragraph = new Paragraph
        {
            Margin = GetThickness($"Markdown.Heading.H{level}.Margin", MarkdownLayoutDefaults.HeadingMargin(level))
        };

        var levelKey = $"H{level}";

        var styleKey = levelKey;

        // 标题字体大小基于基础字体大小按比例缩放
        // 比例系数：H1=1.75, H2=1.42, H3=1.24, H4=1.12, H5=1.02, H6=0.96
        var sizeRatio = level switch
        {
            1 => 1.75,
            2 => 1.42,
            3 => 1.24,
            4 => 1.12,
            5 => 1.02,
            6 => 0.96,
            _ => 1.12
        };
        paragraph.FontSize = _baseFontSize * sizeRatio;
        paragraph.LineHeight = paragraph.FontSize * 1.24;

        // 使用动态资源绑定标题前景色（H3 使用 H2 的颜色，默认深色主题）
        SetDynamicResource(paragraph, Paragraph.ForegroundProperty,
            $"Markdown.Heading.{styleKey}.Foreground",
            new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)));

        paragraph.FontWeight = level switch
        {
            <= 2 => FontWeights.Bold,
            3 => FontWeights.SemiBold,
            _ => FontWeights.Medium
        };

        // H1, H2 和 H3 添加底部边框
        if (level <= 3)
        {
            // 使用动态资源绑定边框颜色（H3 使用 H2 的边框颜色，默认深色主题）
            SetDynamicResource(paragraph, Paragraph.BorderBrushProperty,
                $"Markdown.Heading.{styleKey}.Border",
                new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
            paragraph.BorderThickness = GetThickness("Markdown.Heading.BorderThickness", MarkdownLayoutDefaults.HeadingBorderThickness);
            paragraph.Padding = GetThickness($"Markdown.Heading.{styleKey}.Padding",
                MarkdownLayoutDefaults.HeadingPadding(level));
        }

        // 转换内联内容
        if (heading.Inline != null)
        {
            foreach (var inline in heading.Inline)
            {
                var wpfInline = ConvertInline(inline);
                if (wpfInline != null)
                    paragraph.Inlines.Add(wpfInline);
            }
        }

        return paragraph;
    }

    /// <summary>
    /// 转换段落块
    /// </summary>
    private WpfBlock ConvertParagraph(ParagraphBlock paragraph)
    {
        var wpfParagraph = new Paragraph
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin),
            TextAlignment = TextAlignment.Left
        };

        if (paragraph.Inline != null)
        {
            foreach (var inline in paragraph.Inline)
            {
                var wpfInline = ConvertInline(inline);
                if (wpfInline != null)
                    wpfParagraph.Inlines.Add(wpfInline);
            }
        }

        return wpfParagraph;
    }

    /// <summary>
    /// 转换引用块
    /// </summary>
    private WpfBlock ConvertQuote(QuoteBlock quote, bool enableSyntaxHighlighting, CodeBlockRenderer codeBlockRenderer)
    {
        var section = new Section
        {
            BorderThickness = GetThickness("Markdown.Quote.BorderThickness", MarkdownLayoutDefaults.QuoteBorderThickness),
            Padding = GetThickness("Markdown.Quote.Padding", MarkdownLayoutDefaults.QuotePadding),
            Margin = GetThickness("Markdown.Quote.Margin", MarkdownLayoutDefaults.QuoteMargin)
        };

        // 使用动态资源绑定引用块的背景和边框颜色（默认深色主题）
        SetDynamicResource(section, Section.BackgroundProperty,
            "Markdown.Quote.Background",
            new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)));
        SetDynamicResource(section, Section.BorderBrushProperty,
            "Markdown.Quote.Border",
            new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
        SetDynamicResource(section, Section.ForegroundProperty,
            "Markdown.Quote.Foreground",
            new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)));

        // 递归转换子块
        foreach (var block in quote)
        {
            var element = ConvertBlock(block, enableSyntaxHighlighting, codeBlockRenderer);
            if (element != null)
                section.Blocks.Add(element);
        }

        return section;
    }

    /// <summary>
    /// 转换列表块
    /// </summary>
    private WpfBlock ConvertList(ListBlock list, bool enableSyntaxHighlighting, CodeBlockRenderer codeBlockRenderer, int listLevel = 0)
    {
        var wpfList = list.IsOrdered ? (WpfBlock)new List() : new List();

        if (wpfList is List listElement)
        {
            // 调整列表缩进：有序/无序分别处理，避免有序列表序号与正文间距异常
            var leftPadding = list.IsOrdered
                ? (listLevel == 0 ? 20 : 16 + (listLevel - 1) * 8)
                : (listLevel == 0 ? 20 : 12 + (listLevel - 1) * 8);
            listElement.Margin = GetThickness("Markdown.List.Margin", MarkdownLayoutDefaults.ListMargin);
            listElement.Padding = new Thickness(leftPadding, 0, 0, 0);

            if (list.IsOrdered)
            {
                listElement.MarkerStyle = TextMarkerStyle.Decimal;
                listElement.FontSize = _baseFontSize * 0.94;
            }
            else
            {
                // 无序列表按层级区分标记样式和尺寸，避免二级圆圈过大
                listElement.MarkerStyle = listLevel switch
                {
                    0 => TextMarkerStyle.Disc,
                    1 => TextMarkerStyle.Circle,
                    _ => TextMarkerStyle.Square
                };

                var markerScale = listLevel switch
                {
                    0 => 1.0,
                    1 => 0.45,
                    _ => 0.74
                };
                listElement.FontSize = _baseFontSize * markerScale;
            }

            // 控制 marker 偏移，避免有序列表“编号和内容空格太大”
            listElement.MarkerOffset = listLevel == 0 ? 2 : 3;
            var orderedStart = 1;
            if (list.IsOrdered
                && int.TryParse(list.OrderedStart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart))
            {
                orderedStart = Math.Max(1, parsedStart);
            }

            listElement.StartIndex = orderedStart;

            // 不设置 Foreground，让标记继承 FlowDocument 的前景色

            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    var listItemElement = new ListItem
                    {
                        Margin = GetThickness("Markdown.List.Item.Margin", MarkdownLayoutDefaults.ListItemMargin),
                        Padding = new Thickness(0, 0, 0, 0)
                    };

                    foreach (var block in listItem)
                    {
                        // 传递 listLevel + 1 给嵌套列表
                        var element = ConvertBlock(block, enableSyntaxHighlighting, codeBlockRenderer, listLevel + 1);
                        if (element != null)
                        {
                            // 减少列表项内部段落的 Margin，避免间距过大
                            if (element is Paragraph para)
                            {
                                para.Margin = new Thickness(0, 0, 0, 0);
                                para.FontSize = _baseFontSize;
                                para.LineHeight = _baseFontSize * GetDouble("Markdown.LineHeight", MarkdownLayoutDefaults.LineHeightScale);
                            }
                            listItemElement.Blocks.Add(element);
                        }
                    }

                    listElement.ListItems.Add(listItemElement);
                }
            }
        }

        return wpfList;
    }

    /// <summary>
    /// 转换代码块
    /// </summary>
    private WpfBlock ConvertCodeBlock(CodeBlock codeBlock, CodeBlockRenderer codeBlockRenderer)
    {
        // 获取代码内容和语言
        var code = codeBlock is FencedCodeBlock fenced ? fenced.Lines.ToString() :
                   codeBlock is CodeBlock cb ? cb.Lines.ToString() : string.Empty;
        var language = codeBlock is FencedCodeBlock fencedCode ? fencedCode.Info : string.Empty;

        return codeBlockRenderer.Render(code, language);
    }

    /// <summary>
    /// 转换表格
    /// </summary>
    private WpfBlock ConvertTable(MarkdigTable table, bool enableSyntaxHighlighting, CodeBlockRenderer codeBlockRenderer)
    {
        var wpfTable = new System.Windows.Documents.Table
        {
            CellSpacing = 0,
            Margin = GetThickness("Markdown.Table.Margin", MarkdownLayoutDefaults.TableMargin),
            BorderThickness = MarkdownLayoutDefaults.TableBorderThickness
        };

        SetDynamicResource(wpfTable, System.Windows.Documents.Table.BorderBrushProperty,
            "Markdown.Table.Border",
            new SolidColorBrush(Color.FromRgb(0xD5, 0xDE, 0xE9)));
        SetDynamicResource(wpfTable, System.Windows.Documents.Table.BackgroundProperty,
            "Markdown.Table.Background",
            Brushes.Transparent);

        var columnCount = GetTableColumnCount(table);
        for (var i = 0; i < columnCount; i++)
        {
            wpfTable.Columns.Add(new TableColumn());
        }

        var rowGroup = new TableRowGroup();
        foreach (var rowBlock in table)
        {
            if (rowBlock is not MarkdigTableRow row)
            {
                continue;
            }

            var wpfRow = new System.Windows.Documents.TableRow();
            foreach (var cellBlock in row)
            {
                if (cellBlock is not MarkdigTableCell cell)
                {
                    continue;
                }

                var wpfCell = new System.Windows.Documents.TableCell
                {
                    Padding = GetThickness("Markdown.Table.Cell.Padding", MarkdownLayoutDefaults.TableCellPadding),
                    BorderThickness = MarkdownLayoutDefaults.TableCellBorderThickness,
                    TextAlignment = GetTableTextAlignment(table, cell.ColumnIndex)
                };

                SetDynamicResource(wpfCell, System.Windows.Documents.TableCell.BorderBrushProperty,
                    "Markdown.Table.Border",
                    new SolidColorBrush(Color.FromRgb(0xD5, 0xDE, 0xE9)));
                SetDynamicResource(wpfCell, System.Windows.Documents.TableCell.ForegroundProperty,
                    "Markdown.Foreground",
                    new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)));

                if (row.IsHeader)
                {
                    SetDynamicResource(wpfCell, System.Windows.Documents.TableCell.BackgroundProperty,
                        "Markdown.Table.Header.Background",
                        new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6)));
                    SetDynamicResource(wpfCell, System.Windows.Documents.TableCell.ForegroundProperty,
                        "Markdown.Table.Header.Foreground",
                        new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)));
                    wpfCell.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    SetDynamicResource(wpfCell, System.Windows.Documents.TableCell.BackgroundProperty,
                        "Markdown.Table.Row.Background",
                        new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)));
                }

                if (cell.ColumnSpan > 1)
                {
                    wpfCell.ColumnSpan = cell.ColumnSpan;
                }

                if (cell.RowSpan > 1)
                {
                    wpfCell.RowSpan = cell.RowSpan;
                }

                var hasContent = false;
                foreach (var cellChild in cell)
                {
                    var converted = ConvertBlock(cellChild, enableSyntaxHighlighting, codeBlockRenderer);
                    if (converted == null)
                    {
                        continue;
                    }

                    if (converted is Paragraph paragraph)
                    {
                        paragraph.Margin = new Thickness(0);
                        paragraph.LineHeight = _baseFontSize * GetDouble("Markdown.LineHeight", MarkdownLayoutDefaults.LineHeightScale);
                    }

                    wpfCell.Blocks.Add(converted);
                    hasContent = true;
                }

                if (!hasContent)
                {
                    wpfCell.Blocks.Add(new Paragraph(new Run(string.Empty)) { Margin = new Thickness(0) });
                }

                wpfRow.Cells.Add(wpfCell);
            }

            if (wpfRow.Cells.Count > 0)
            {
                rowGroup.Rows.Add(wpfRow);
            }
        }

        if (rowGroup.Rows.Count > 0)
        {
            wpfTable.RowGroups.Add(rowGroup);
        }

        return wpfTable;
    }

    private static int GetTableColumnCount(MarkdigTable table)
    {
        if (table.ColumnDefinitions != null && table.ColumnDefinitions.Count > 0)
        {
            return table.ColumnDefinitions.Count;
        }

        var maxColumns = 0;
        foreach (var rowBlock in table)
        {
            if (rowBlock is not MarkdigTableRow row)
            {
                continue;
            }

            var rowColumns = 0;
            foreach (var cellBlock in row)
            {
                if (cellBlock is MarkdigTableCell cell)
                {
                    rowColumns += Math.Max(1, cell.ColumnSpan);
                }
            }

            maxColumns = Math.Max(maxColumns, rowColumns);
        }

        return Math.Max(1, maxColumns);
    }

    private static TextAlignment GetTableTextAlignment(MarkdigTable table, int columnIndex)
    {
        if (table.ColumnDefinitions == null || columnIndex < 0 || columnIndex >= table.ColumnDefinitions.Count)
        {
            return TextAlignment.Left;
        }

        var alignment = table.ColumnDefinitions[columnIndex].Alignment;
        if (!alignment.HasValue)
        {
            return TextAlignment.Left;
        }

        return alignment.Value switch
        {
            MarkdigTableColumnAlign.Center => TextAlignment.Center,
            MarkdigTableColumnAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }

    /// <summary>
    /// 转换水平分隔线
    /// </summary>
    private WpfBlock ConvertThematicBreak()
    {
        var paragraph = new Paragraph
        {
            Margin = GetThickness("Markdown.HorizontalRule.Margin", MarkdownLayoutDefaults.HorizontalRuleMargin),
            BorderBrush = GetBrush("Markdown.HorizontalRule.Border", Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderThickness = GetThickness("Markdown.HorizontalRule.BorderThickness", MarkdownLayoutDefaults.HorizontalRuleBorderThickness),
            Padding = new Thickness(0)
        };

        return paragraph;
    }

    #endregion

    #region Inline 转换

    /// <summary>
    /// 将 Markdig Inline 转换为 WPF Inline
    /// </summary>
    private WpfInline? ConvertInline(MarkdigInline inline)
    {
        return inline switch
        {
            LiteralInline literal => ConvertLiteral(literal),
            MathInline math => new Run(math.ToString() ?? string.Empty),
            FootnoteLink footnoteLink => new Run(footnoteLink.ToString() ?? string.Empty),
            EmphasisInline emphasis => ConvertEmphasis(emphasis),
            CodeInline code => ConvertInlineCode(code),
            LineBreakInline => new LineBreak(),
            AutolinkInline autolink => ConvertAutolink(autolink),
            HtmlInline html => new Run(html.Tag ?? string.Empty),
            TaskList task => ConvertTaskList(task),
            LinkInline link => ConvertLink(link),
            _ => ConvertUnknownInline(inline)
        };
    }

    private WpfInline ConvertUnknownInline(MarkdigInline inline)
    {
        var span = new Span();
        if (inline is ContainerInline container)
        {
            AppendInlineChildren(container, span.Inlines);
        }

        if (span.Inlines.Count == 0)
        {
            span.Inlines.Add(new Run(inline.ToString() ?? string.Empty));
        }

        System.Diagnostics.Debug.WriteLine($"[MarkdownRenderer] Unsupported inline {inline.GetType().FullName}; rendered as text.");
        return span;
    }

    /// <summary>
    /// 转换文本内容
    /// </summary>
    private WpfInline ConvertLiteral(LiteralInline literal)
    {
        var text = literal.Content.ToString();
        return new Run(text);
    }

    /// <summary>
    /// 转换强调(粗体/斜体)
    /// </summary>
    private WpfInline ConvertEmphasis(EmphasisInline emphasis)
    {
        var span = new Span();

        if (emphasis.DelimiterChar == '~')
        {
            span.TextDecorations = TextDecorations.Strikethrough;
        }
        // 粗体 (** 或 __)
        else if (emphasis.DelimiterCount >= 2)
        {
            span.FontWeight = FontWeights.Bold;
            span.Foreground = GetBrush("Markdown.Bold.Foreground", Color.FromRgb(0x4C, 0x63, 0xEB));
        }
        // 斜体 (*)
        else if (emphasis.DelimiterCount == 1)
        {
            span.FontStyle = FontStyles.Italic;
        }

        // 递归转换子元素
        foreach (var child in emphasis)
        {
            var childElement = ConvertInline(child);
            if (childElement != null)
                span.Inlines.Add(childElement);
        }

        return span;
    }

    private WpfInline ConvertTaskList(TaskList task)
    {
        var checkBox = new CheckBox
        {
            IsChecked = task.Checked,
            IsEnabled = false,
            IsHitTestVisible = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = GetThickness("Markdown.TaskList.Margin", MarkdownLayoutDefaults.TaskListMargin),
            MinWidth = 14,
            MinHeight = 14
        };
        System.Windows.Automation.AutomationProperties.SetName(
            checkBox, task.Checked == true ? "已完成任务" : "未完成任务");
        return new InlineUIContainer(checkBox) { BaselineAlignment = BaselineAlignment.Center };
    }

    /// <summary>
    /// 转换行内代码
    /// </summary>
    private WpfInline ConvertInlineCode(CodeInline code)
    {
        var inlineFontSize = _baseFontSize * GetDouble(
            "Markdown.InlineCode.FontScale",
            MarkdownLayoutDefaults.InlineCodeFontScale);
        var codeText = new System.Windows.Controls.TextBlock
        {
            Text = code.Content,
            FontFamily = GetFontFamily("Markdown.CodeFontFamily", "Consolas, Monaco, Courier New, monospace"),
            FontSize = inlineFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            LineHeight = inlineFontSize * GetDouble(
                "Markdown.InlineCode.LineHeightScale",
                MarkdownLayoutDefaults.InlineCodeLineHeightScale),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        SetDynamicResource(codeText, System.Windows.Controls.TextBlock.ForegroundProperty,
            "Markdown.InlineCode.Foreground",
            new SolidColorBrush(Color.FromRgb(0xF9, 0x82, 0x66)));

        var textHost = new Grid
        {
            MinHeight = Math.Ceiling(inlineFontSize * GetDouble(
                "Markdown.InlineCode.MinHeightScale",
                MarkdownLayoutDefaults.InlineCodeMinHeightScale)),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        textHost.Children.Add(codeText);

        var border = new Border
        {
            Child = textHost,
            Padding = GetThickness("Markdown.InlineCode.Padding", MarkdownLayoutDefaults.InlineCodePadding),
            CornerRadius = GetCornerRadius("Markdown.InlineCode.CornerRadius", MarkdownLayoutDefaults.InlineCodeCornerRadius),
            BorderThickness = GetThickness("Markdown.InlineCode.BorderThickness", MarkdownLayoutDefaults.TableBorderThickness),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        // 使用动态资源绑定内联代码的背景和前景色（默认深色主题）
        SetDynamicResource(border, Border.BackgroundProperty,
            "Markdown.InlineCode.Background",
            new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E)));
        SetDynamicResource(border, Border.BorderBrushProperty,
            "Markdown.InlineCode.Border",
            new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));

        return new InlineUIContainer(border)
        {
            BaselineAlignment = BaselineAlignment.Center
        };
    }

    /// <summary>
    /// 转换链接
    /// </summary>
    private WpfInline ConvertLink(LinkInline link)
    {
        // 如果是图片链接，创建图片
        if (link.IsImage)
        {
            return ConvertImage(link);
        }

        if (!TryCreateSafeNavigateUri(link.Url, out var safeUri))
        {
            var fallbackSpan = new Span();
            AppendInlineChildren(link, fallbackSpan.Inlines);
            return fallbackSpan;
        }

        var hyperlink = CreateHyperlink(safeUri!);

        // 转换链接文本
        AppendInlineChildren(link, hyperlink.Inlines);

        return hyperlink;
    }

    private WpfInline ConvertAutolink(AutolinkInline link)
    {
        var url = link.Url ?? string.Empty;
        if (link.IsEmail && !url.Contains(":", StringComparison.Ordinal))
        {
            url = "mailto:" + url;
        }

        if (!TryCreateSafeNavigateUri(url, out var safeUri))
        {
            return new Run(link.Url ?? string.Empty);
        }

        var hyperlink = CreateHyperlink(safeUri!);
        hyperlink.Inlines.Add(new Run(link.Url ?? string.Empty));
        return hyperlink;
    }

    private Hyperlink CreateHyperlink(Uri uri)
    {
        var hyperlink = new Hyperlink
        {
            NavigateUri = uri,
            TextDecorations = null,
            FontWeight = FontWeights.SemiBold
        };
        SetDynamicResource(hyperlink, Hyperlink.ForegroundProperty,
            "Markdown.Link.Foreground",
            new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
        hyperlink.RequestNavigate += OnRequestNavigate;
        return hyperlink;
    }

    private void OnRequestNavigate(object? sender, RequestNavigateEventArgs e)
    {
        _linkNavigator.Handle(e);
    }

    internal static bool TryCreateSafeNavigateUri(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 4096)
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        var isMailto = string.Equals(candidate.Scheme, Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
        if (!WpfMarkdownLinkNavigator.IsAllowedScheme(candidate.Scheme)
            || (!isMailto && !string.IsNullOrEmpty(candidate.UserInfo))
            || (!isMailto && string.IsNullOrWhiteSpace(candidate.Host))
            || (isMailto && (string.IsNullOrWhiteSpace(candidate.Host)
                || string.IsNullOrWhiteSpace(candidate.UserInfo))))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    internal WpfMarkdownLinkNavigator LinkNavigator => _linkNavigator;

    internal WpfMarkdownImageLoader ImageLoaderAdapter => _wpfImageLoader;

    internal CodeBlockRenderer CreateCodeBlockRenderer(MarkdownRenderOptions options)
        => options.CodeBlockRenderer
            ?? new CodeBlockRenderer(
                options.EnableSyntaxHighlighting,
                themeMode: ThemeManager.GetCurrentTheme(),
                baseFontSize: options.FontSize,
                clipboardService: _clipboardService,
                syntaxHighlighter: _syntaxHighlighter);

    private void AppendInlineChildren(ContainerInline source, InlineCollection target)
    {
        if (source.FirstChild == null)
        {
            return;
        }

        foreach (var child in source)
        {
            var childElement = ConvertInline(child);
            if (childElement != null)
            {
                target.Add(childElement);
            }
        }
    }

    /// <summary>
    /// 转换图片
    /// </summary>
    private WpfInline ConvertImage(LinkInline image)
    {
        if (string.IsNullOrEmpty(image.Url))
            return new Run("[图片加载失败]");

        if (!TryCreateSafeImageUri(image.Url, out var imageUri))
        {
            return new Run("[图片已阻止: 仅允许 http/https 地址]") { FontStyle = FontStyles.Italic };
        }

        var renderSession = _activeRenderSession;
        if (renderSession == null
            || !renderSession.TryReserveImage(_activeMaxImagesPerDocument))
        {
            return new Run("[图片数量超出限制]") { FontStyle = FontStyles.Italic };
        }

        try
        {
            var placeholder = new System.Windows.Controls.TextBlock
            {
                Text = "[图片加载中...]",
                FontStyle = FontStyles.Italic,
                Margin = GetThickness("Markdown.Image.Placeholder.Margin", MarkdownLayoutDefaults.ImagePlaceholderMargin)
            };
            var border = new Border
            {
                Background = GetBrush("Markdown.Image.Background", Colors.Transparent),
                BorderBrush = GetBrush("Markdown.Image.Border", Color.FromRgb(0xCC, 0xCC, 0xCC)),
                BorderThickness = GetThickness("Markdown.Image.BorderThickness", MarkdownLayoutDefaults.ImageBorderThickness),
                CornerRadius = GetCornerRadius("Markdown.Image.CornerRadius", MarkdownLayoutDefaults.ImageCornerRadius),
                Padding = GetThickness("Markdown.Image.Padding", MarkdownLayoutDefaults.ImagePadding),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            border.Child = placeholder;
            _ = _wpfImageLoader.LoadIntoAsync(
                border,
                imageUri!,
                placeholder,
                _activeImageLoadOptions,
                GetThickness("Markdown.Image.Margin", MarkdownLayoutDefaults.ImageMargin),
                renderSession.Cancellation.Token);

            // 如果有替代文本，作为工具提示
            if (image.FirstChild != null)
            {
                var tooltip = new ToolTip();
                var tooltipTextBlock = new System.Windows.Controls.TextBlock
                {
                    Text = image.FirstChild.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = GetDouble("Markdown.Image.TooltipMaxWidth", MarkdownLayoutDefaults.ImageTooltipMaxWidth)
                };
                tooltip.Content = tooltipTextBlock;
                border.ToolTip = tooltip;
            }

            return new InlineUIContainer(border);
        }
        catch (Exception ex)
        {
            return new Run($"[图片加载失败: {ex.Message}]")
            {
                Foreground = GetBrush("Markdown.Error.Foreground", Colors.Red),
                FontStyle = FontStyles.Italic
            };
        }
    }

    internal static bool TryCreateSafeImageUri(string? url, out Uri? uri)
        => MarkdownImageSecurity.TryCreateSafeImageUri(url, out uri);

    #endregion

    #region 主题资源辅助方法

    /// <summary>
    /// 设置动态资源引用（用于 FrameworkContentElement）
    /// </summary>
    private void SetDynamicResource(FrameworkContentElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        var resource = Application.Current?.TryFindResource(resourceKey);
        if (resource != null)
        {
            element.SetResourceReference(property, resourceKey);
            return;
        }

        element.SetValue(property, defaultValue);
    }

    /// <summary>
    /// 设置动态资源引用（用于 TextElement）
    /// </summary>
    private void SetDynamicResource(System.Windows.Documents.TextElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        var resource = Application.Current?.TryFindResource(resourceKey);
        if (resource != null)
        {
            element.SetResourceReference(property, resourceKey);
            return;
        }

        element.SetValue(property, defaultValue);
    }

    /// <summary>
    /// 设置动态资源引用（用于 FrameworkElement）
    /// </summary>
    private void SetDynamicResource(FrameworkElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        var resource = Application.Current?.TryFindResource(resourceKey);
        if (resource != null)
        {
            element.SetResourceReference(property, resourceKey);
            return;
        }

        element.SetValue(property, defaultValue);
    }

    private Brush GetBrush(string key, Color defaultColor)
    {
        return Application.Current?.TryFindResource(key) is Brush brush
            ? brush
            : new SolidColorBrush(defaultColor);
    }

    private FontFamily GetFontFamily(string key, string defaultFont)
    {
        return Application.Current?.TryFindResource(key) is FontFamily fontFamily
            ? fontFamily
            : new FontFamily(defaultFont);
    }

    private double GetDouble(string key, double defaultValue)
    {
        return Application.Current?.TryFindResource(key) is double value
            && value > 0
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            ? value
            : defaultValue;
    }

    private Thickness GetThickness(string key, Thickness defaultThickness)
    {
        return Application.Current?.TryFindResource(key) is Thickness thickness
            ? thickness
            : defaultThickness;
    }

    private CornerRadius GetCornerRadius(string key, CornerRadius defaultCornerRadius)
    {
        return Application.Current?.TryFindResource(key) is CornerRadius cornerRadius
            ? cornerRadius
            : defaultCornerRadius;
    }

    #endregion
}
