using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using Markdig;
using MarkdView.Enums;
using MarkdView.Interactions;
using MarkdView.Media;
using MarkdView.Parsing;
using MarkdView.Renderers;
using MarkdView.Services;

namespace MarkdView.Controls;

/// <summary>
/// MarkdView - 现代化 WPF Markdown 渲染控件
/// 基于 MVVM 架构，支持流式渲染、语法高亮、主题切换
/// </summary>
public class MarkdownViewer : ContentControl
{
    static MarkdownViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MarkdownViewer),
            new FrameworkPropertyMetadata(typeof(MarkdownViewer)));

        // 直接在代码中设置模板（作为后备方案）
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        factory.AppendChild(gridFactory);

        var scrollViewerFactory = new FrameworkElementFactory(typeof(FlowDocumentScrollViewer));
        scrollViewerFactory.Name = "PART_MarkdownDocument";
        scrollViewerFactory.SetValue(FlowDocumentScrollViewer.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        scrollViewerFactory.SetValue(FlowDocumentScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scrollViewerFactory.SetValue(FlowDocumentScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scrollViewerFactory.SetValue(FlowDocumentScrollViewer.IsToolBarVisibleProperty, false);
        gridFactory.AppendChild(scrollViewerFactory);

        var template = new ControlTemplate(typeof(MarkdownViewer)) { VisualTree = factory };

        var style = new Style(typeof(MarkdownViewer));
        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.Transparent));

        StyleProperty.OverrideMetadata(typeof(MarkdownViewer), new FrameworkPropertyMetadata(style));
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        var markdownText = newContent as string ?? string.Empty;

        // 保存最新期望文本。空字符串也是有效内容，不能用“非空”判断是否需要渲染。
        _latestText = markdownText;

        // 如果MarkdownDocument还没初始化，等待OnApplyTemplate
        if (MarkdownDocument == null)
        {
            return;
        }

        // 在列表场景中（滚动条禁用），总是立即渲染
        var isListScenario = VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;

        if (EnableStreaming && !isListScenario)
        {
            // 自适应防抖由 coordinator 在解析前执行，控件只提供策略值。
            var adaptiveThrottle = CalculateAdaptiveThrottle(markdownText.Length);
            RequestRender(adaptiveThrottle);
        }
        else
        {
            // 立即渲染
            RequestRender();
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Foreground 可从父级继承；变化时需要刷新已创建的 FlowDocument 快照。
        if (e.Property == ForegroundProperty && MarkdownDocument != null)
        {
            RequestRender();
        }
    }

    #region 依赖属性

    public static readonly DependencyProperty EnableStreamingProperty =
        DependencyProperty.Register(
            nameof(EnableStreaming),
            typeof(bool),
            typeof(MarkdownViewer),
            new PropertyMetadata(true, OnEnableStreamingChanged));

    public static readonly DependencyProperty StreamingThrottleProperty =
        DependencyProperty.Register(
            nameof(StreamingThrottle),
            typeof(int),
            typeof(MarkdownViewer),
            new FrameworkPropertyMetadata(50, OnStreamingThrottleChanged),
            ValidateStreamingThrottle);

    public static readonly DependencyProperty EnableSyntaxHighlightingProperty =
        DependencyProperty.Register(
            nameof(EnableSyntaxHighlighting),
            typeof(bool),
            typeof(MarkdownViewer),
            new PropertyMetadata(true, OnEnableSyntaxHighlightingChanged));

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(
            nameof(Theme),
            typeof(ThemeMode),
            typeof(MarkdownViewer),
            new PropertyMetadata(ThemeMode.Auto, OnThemeChanged),
            ValidateTheme);

    public new static readonly DependencyProperty FontFamilyProperty =
        Control.FontFamilyProperty.AddOwner(
            typeof(MarkdownViewer),
            new FrameworkPropertyMetadata(
                new FontFamily("Noto Sans, SF Pro SC, SF Pro Text, SF Pro Icons, PingFang SC, Helvetica Neue, Helvetica, Arial"),
                FrameworkPropertyMetadataOptions.Inherits,
                OnFontFamilyChanged,
                CoerceFontFamily));

    public new static readonly DependencyProperty FontSizeProperty =
        Control.FontSizeProperty.AddOwner(
            typeof(MarkdownViewer),
            new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.Inherits, OnFontSizeChanged, CoerceFontSize));

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(VerticalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(MarkdownViewer),
            new PropertyMetadata(ScrollBarVisibility.Auto, OnVerticalScrollBarVisibilityChanged));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(
            nameof(HorizontalScrollBarVisibility),
            typeof(ScrollBarVisibility),
            typeof(MarkdownViewer),
            new PropertyMetadata(ScrollBarVisibility.Auto, OnHorizontalScrollBarVisibilityChanged));

    public static readonly DependencyProperty UseTransparentCanvasProperty =
        DependencyProperty.Register(
            nameof(UseTransparentCanvas),
            typeof(bool),
            typeof(MarkdownViewer),
            new PropertyMetadata(false, OnUseTransparentCanvasChanged));

    public static readonly DependencyProperty ImageLoadTimeoutProperty =
        DependencyProperty.Register(
            nameof(ImageLoadTimeout),
            typeof(TimeSpan),
            typeof(MarkdownViewer),
            new PropertyMetadata(MarkdownRenderDefaults.ImageLoadTimeout, OnImageConfigurationChanged),
            ValidateImageLoadTimeout);

    public static readonly DependencyProperty MaxImageBytesProperty =
        DependencyProperty.Register(
            nameof(MaxImageBytes),
            typeof(long),
            typeof(MarkdownViewer),
            new PropertyMetadata(MarkdownRenderDefaults.MaxImageBytes, OnImageConfigurationChanged),
            ValidateMaxImageBytes);

    public static readonly DependencyProperty MaxImagesPerDocumentProperty =
        DependencyProperty.Register(
            nameof(MaxImagesPerDocument),
            typeof(int),
            typeof(MarkdownViewer),
            new PropertyMetadata(MarkdownRenderDefaults.MaxImagesPerDocument, OnImageConfigurationChanged),
            ValidateMaxImagesPerDocument);

    public static readonly DependencyProperty MaxImageDecodePixelProperty =
        DependencyProperty.Register(
            nameof(MaxImageDecodePixel),
            typeof(int),
            typeof(MarkdownViewer),
            new PropertyMetadata(MarkdownRenderDefaults.MaxImageDecodePixel, OnImageConfigurationChanged),
            ValidateMaxImageDecodePixel);

    #endregion

    #region 公共属性

    /// <summary>
    /// 渲染完成事件 - 当 Markdown 渲染完成时触发
    /// </summary>
    public event EventHandler? RenderCompleted;

    /// <summary>
    /// 渲染失败事件。失败时仍会触发 RenderCompleted，以便调用方结束 loading 状态。
    /// </summary>
    public event EventHandler<MarkdownRenderFailedEventArgs>? RenderFailed;

    public bool EnableStreaming
    {
        get => (bool)GetValue(EnableStreamingProperty);
        set => SetValue(EnableStreamingProperty, value);
    }

    public int StreamingThrottle
    {
        get => (int)GetValue(StreamingThrottleProperty);
        set => SetValue(StreamingThrottleProperty, value);
    }

    public bool EnableSyntaxHighlighting
    {
        get => (bool)GetValue(EnableSyntaxHighlightingProperty);
        set => SetValue(EnableSyntaxHighlightingProperty, value);
    }

    public ThemeMode Theme
    {
        get => (ThemeMode)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public new FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public new double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// 垂直滚动条可见性
    /// </summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// 水平滚动条可见性
    /// </summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>
    /// 是否使用透明画布背景。默认 false，使用主题定义的 Markdown.Background。
    /// </summary>
    public bool UseTransparentCanvas
    {
        get => (bool)GetValue(UseTransparentCanvasProperty);
        set => SetValue(UseTransparentCanvasProperty, value);
    }

    /// <summary>
    /// 单张图片的加载超时。变更会创建新的请求配置快照。
    /// </summary>
    public TimeSpan ImageLoadTimeout
    {
        get => (TimeSpan)GetValue(ImageLoadTimeoutProperty);
        set => SetValue(ImageLoadTimeoutProperty, value);
    }

    /// <summary>
    /// 单张图片允许读取的最大字节数。
    /// </summary>
    public long MaxImageBytes
    {
        get => (long)GetValue(MaxImageBytesProperty);
        set => SetValue(MaxImageBytesProperty, value);
    }

    /// <summary>
    /// 单个 Markdown 文档允许启动的最大图片加载数量。设置为 0 表示全部阻止。
    /// </summary>
    public int MaxImagesPerDocument
    {
        get => (int)GetValue(MaxImagesPerDocumentProperty);
        set => SetValue(MaxImagesPerDocumentProperty, value);
    }

    /// <summary>
    /// 图片解码时允许的最大宽/高像素，范围为 1 到 8192。
    /// </summary>
    public int MaxImageDecodePixel
    {
        get => (int)GetValue(MaxImageDecodePixelProperty);
        set => SetValue(MaxImageDecodePixelProperty, value);
    }

    #endregion

    #region 私有字段

    // Markdig 管道
    private readonly MarkdownPipeline _pipeline;

    // 最新文本和本地安装版本；防抖、取消和请求竞争由 MarkdownRenderCoordinator 管理。
    private string _latestText = string.Empty;
    private long _renderVersion;
    private bool _isThemeAppliedSubscribed; // 防止重复订阅静态主题事件
    private bool _isActive;

    // 服务
    private readonly IMarkdownFlowDocumentRenderer _renderingService;
    private readonly MarkdownRenderCoordinator _renderCoordinator;
    private readonly IClipboardService _clipboardService;
    private readonly ISyntaxHighlighter _syntaxHighlighter;
    private readonly IThemeService _themeService;

    // 模板元素
    private FlowDocumentScrollViewer? _markdownDocument;
    private FlowDocumentScrollViewer? MarkdownDocument
    {
        get => _markdownDocument;
        set
        {
            _markdownDocument = value;
        }
    }

    // 缓存父级ScrollViewer，用于滚轮事件处理
    private ScrollViewer? _cachedParentScrollViewer;

    // 用于检测绑定延迟的标志
    private bool _hasCheckedBindingAfterLoad;

    private static bool ValidateStreamingThrottle(object value) => value is int i && i >= 1 && i <= 10000;
    private static bool ValidateFontSize(object value) => value is double d && !double.IsNaN(d) && !double.IsInfinity(d) && d > 0 && d <= 200;
    private static bool ValidateTheme(object value) => value is ThemeMode.Auto or ThemeMode.Light or ThemeMode.Dark;
    private static bool ValidateImageLoadTimeout(object value)
        => value is TimeSpan timeout && timeout > TimeSpan.Zero && timeout <= MarkdownImageLoadOptions.MaxTimeout;
    private static bool ValidateMaxImageBytes(object value)
        => value is long bytes && bytes > 0 && bytes <= MarkdownImageLoadOptions.MaxAllowedBytes;
    private static bool ValidateMaxImagesPerDocument(object value)
        => value is int count && count >= 0 && count <= MarkdownRenderDefaults.MaxImagesPerDocumentLimit;
    private static bool ValidateMaxImageDecodePixel(object value)
        => value is int pixels && pixels > 0 && pixels <= 8192;
    private static object CoerceFontSize(DependencyObject d, object baseValue)
        => ValidateFontSize(baseValue) ? baseValue : 14.0;
    private static object CoerceFontFamily(DependencyObject d, object baseValue)
        => baseValue is FontFamily ? baseValue : new FontFamily("Segoe UI");

    #endregion

    #region 构造函数

    public MarkdownViewer()
        : this(null, null, null, null, null)
    {
    }

    /// <summary>
    /// 保留原有四参数构造函数的元数据，确保已编译宿主仍可加载当前控件。
    /// </summary>
    public MarkdownViewer(
        IMarkdownImageLoader? imageLoader = null,
        IMarkdownLinkHandler? linkHandler = null,
        IClipboardService? clipboardService = null,
        ISyntaxHighlighter? syntaxHighlighter = null)
        : this(imageLoader, linkHandler, clipboardService, syntaxHighlighter, null)
    {
    }

    /// <summary>
    /// 创建可替换副作用实现的 MarkdownViewer。无参构造仍用于 XAML 和默认场景。
    /// </summary>
    public MarkdownViewer(
        IMarkdownImageLoader? imageLoader,
        IMarkdownLinkHandler? linkHandler,
        IClipboardService? clipboardService,
        ISyntaxHighlighter? syntaxHighlighter,
        IThemeService? themeService)
    {

        // 配置 Markdig 管道
        _pipeline = MarkdownPipelineFactory.CreateDefault();

        // 初始化渲染服务
        _clipboardService = clipboardService ?? new WpfClipboardService();
        _syntaxHighlighter = syntaxHighlighter ?? new DefaultSyntaxHighlighter();
        _themeService = themeService ?? WpfThemeService.Default;
        var markdownParser = new MarkdigMarkdownParser(_pipeline);
        var documentParser = new MarkdigMarkdownDocumentParser(markdownParser);
        var markdownRenderer = new MarkdownRenderer(
            _pipeline,
            imageLoader,
            linkHandler,
            parser: markdownParser,
            clipboardService: _clipboardService,
            syntaxHighlighter: _syntaxHighlighter);
        _renderingService = new WpfFlowDocumentRenderer(markdownRenderer);
        _renderCoordinator = new MarkdownRenderCoordinator(documentParser, _renderingService, Dispatcher);

        // 订阅 Loaded 事件，用于延迟初始化
        this.Loaded += OnLoaded;

        // 订阅 DataContextChanged 事件，用于检测数据绑定何时生效
        this.DataContextChanged += OnDataContextChanged;

        // 订阅 LayoutUpdated 事件，用于检测绑定完成
        this.LayoutUpdated += OnLayoutUpdated;

        // 在 MarkdownViewer 级别处理鼠标滚轮事件，确保即使外部容器透明也能工作
        this.PreviewMouseWheel += OnControlPreviewMouseWheel;
        this.Unloaded += OnUnloaded;

        // 主题会通过 OnThemeChanged 回调自动应用，这里不需要手动调用
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 从模板中获取 FlowDocumentScrollViewer
        if (GetTemplateChild("PART_MarkdownDocument") is FlowDocumentScrollViewer documentViewer)
        {
            if (MarkdownDocument != null)
            {
                MarkdownDocument.PreviewMouseWheel -= OnPreviewMouseWheel;
            }

            MarkdownDocument = documentViewer;

            // 处理滚轮事件冒泡
            MarkdownDocument.PreviewMouseWheel += OnPreviewMouseWheel;

            // 同步滚动条可见性
            MarkdownDocument.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
            MarkdownDocument.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;

            // 模板重建后重新提交快照；旧 FlowDocument 可能已脱离视觉树。
            RequestRender();
        }

    }

    #endregion

    #region 依赖属性回调

    private static void OnEnableStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.RequestRender();
        }
    }

    private static void OnStreamingThrottleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 取消旧的防抖请求并用新间隔重新提交，避免变更只影响下一次 Content 更新。
            viewer.RequestRender();
        }
    }

    private static void OnEnableSyntaxHighlightingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 语法高亮设置变化，触发重新渲染
            viewer.RequestRender();
        }
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // Theme 仅表示兼容性的跟随模式，不再从控件级别修改应用资源。
            // 全局主题必须由 ThemeManager.ApplyTheme 统一控制，避免一个控件覆盖其他控件。
            viewer.RequestRender();
        }
    }

    private static void OnFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 字体变化，触发重新渲染
            viewer.RequestRender();
        }
    }

    private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 字号变化，触发重新渲染
            viewer.RequestRender();
        }
    }

    private static void OnVerticalScrollBarVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer.MarkdownDocument != null)
        {
            var visibility = (ScrollBarVisibility)e.NewValue;
            viewer.MarkdownDocument.VerticalScrollBarVisibility = visibility;

            // 禁用滚动条时，需要调整布局行为
            if (visibility == ScrollBarVisibility.Disabled)
            {
                // 让控件自动适应内容大小
                viewer.MarkdownDocument.VerticalAlignment = VerticalAlignment.Stretch;
                viewer.MarkdownDocument.Height = double.NaN; // Auto

            }
            else
            {
                viewer.MarkdownDocument.VerticalAlignment = VerticalAlignment.Stretch;
            }

            if (viewer.MarkdownDocument.Document != null)
            {
                viewer.ConfigureFlowDocument(viewer.MarkdownDocument.Document);
            }
        }
    }

    private static void OnHorizontalScrollBarVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer.MarkdownDocument != null)
        {
            viewer.MarkdownDocument.HorizontalScrollBarVisibility = (ScrollBarVisibility)e.NewValue;
        }
    }

    private static void OnUseTransparentCanvasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.RequestRender();
        }
    }

    #endregion

    #region Markdown 渲染

    /// <summary>
    /// 提交一个不可变渲染快照。最新请求竞争、延迟和取消由 coordinator 统一管理。
    /// </summary>
    private void RequestRender(int debounceMilliseconds = 0)
    {
        if (!_isActive || MarkdownDocument == null || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        var textToRender = _latestText;
        var renderVersion = ++_renderVersion;
        var renderOptions = CreateRenderOptions();
        _ = RenderMarkdownAsync(
            textToRender,
            renderVersion,
            renderOptions,
            TimeSpan.FromMilliseconds(Math.Max(0, debounceMilliseconds)));
    }

    private static void OnImageConfigurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.RequestRender();
        }
    }

    private async Task RenderMarkdownAsync(
        string textToRender,
        long renderVersion,
        MarkdownRenderOptions renderOptions,
        TimeSpan debounce)
    {
        if (!_isActive || MarkdownDocument == null)
        {
            return;
        }

        try
        {
            var flowDocument = await _renderCoordinator.RenderLatestAsync(
                textToRender,
                renderOptions,
                debounce);
            if (flowDocument == null
                || renderVersion != _renderVersion
                || !_isActive
                || MarkdownDocument == null)
            {
                return;
            }

            MarkdownDocument.Document = flowDocument;
            ConfigureFlowDocument(flowDocument);
            RaiseRenderCompleted();
        }
        catch (Exception ex)
        {
            // 只有当前快照仍然有效时才显示错误。过期快照的失败不应覆盖新内容。
            if (renderVersion == _renderVersion && _isActive && MarkdownDocument != null)
            {
                ShowErrorDocument(ex.Message);
                RaiseRenderFailed(ex);
                RaiseRenderCompleted();
            }
        }
    }

    private MarkdownRenderOptions CreateRenderOptions()
    {
        var fontSize = FontSize;
        return new MarkdownRenderOptions(FontFamily, fontSize)
        {
            EnableSyntaxHighlighting = EnableSyntaxHighlighting,
            UseTransparentCanvas = UseTransparentCanvas,
            Foreground = Foreground,
            ImageLoadOptions = new MarkdownImageLoadOptions(ImageLoadTimeout, MaxImageBytes)
            {
                MaxDecodePixel = MaxImageDecodePixel
            },
            MaxImagesPerDocument = MaxImagesPerDocument,
            CodeBlockRenderer = new Renderers.CodeBlockRenderer(
                EnableSyntaxHighlighting,
                _themeService.CurrentTheme,
                fontSize,
                _clipboardService,
                _syntaxHighlighter)
        };
    }

    private void RaiseRenderCompleted()
    {
        foreach (EventHandler handler in RenderCompleted?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MarkdownViewer] RenderCompleted handler failed: {ex}"); }
        }
    }

    private void RaiseRenderFailed(Exception exception)
    {
        foreach (EventHandler<MarkdownRenderFailedEventArgs> handler in RenderFailed?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { handler(this, new MarkdownRenderFailedEventArgs(exception)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MarkdownViewer] RenderFailed handler failed: {ex}"); }
        }
    }

    private void ShowErrorDocument(string errorMessage)
    {
        if (MarkdownDocument == null) return;

        try
        {

            // 渲染错误时显示错误信息
            var errorDocument = new FlowDocument();
            var errorParagraph = new Paragraph(new Run($"Markdown 渲染错误: {errorMessage}"))
            {
                Foreground = Application.Current?.TryFindResource("Markdown.Error.Foreground") as Brush
                    ?? Brushes.Red
            };
            errorDocument.Blocks.Add(errorParagraph);

            // 设置Document
            MarkdownDocument.Document = errorDocument;

            // 配置错误文档
            ConfigureFlowDocument(errorDocument);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MarkdownViewer] ShowErrorDocument failed: {ex}");
        }
    }

    /// <summary>
    /// 配置 FlowDocument 的页面属性，以支持列表场景
    /// </summary>
    private void ConfigureFlowDocument(FlowDocument document)
    {
        if (document == null || MarkdownDocument == null) return;

        // 如果禁用了垂直滚动条（列表场景），设置页面为自动高度并去除内边距。
        if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            document.PageHeight = double.NaN; // Auto
            document.PageWidth = double.NaN; // Auto
            document.PagePadding = new Thickness(0);

            // 强制FlowDocumentScrollViewer更新布局
            MarkdownDocument.InvalidateMeasure();
            MarkdownDocument.InvalidateArrange();
            MarkdownDocument.UpdateLayout();
        }
        else
        {
            // 从列表模式恢复时，重新应用主题内边距，避免永久保持 0。
            document.PagePadding = Application.Current?.TryFindResource("Markdown.PagePadding") is Thickness padding
                ? padding
                : MarkdownLayoutDefaults.PagePadding;
        }
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 主题应用完成事件 - ThemeManager 替换资源字典后触发
    /// </summary>
    private void OnThemeApplied(object? sender, EventArgs e)
    {
        // 自定义主题服务可能在后台线程发出通知；DependencyObject 状态只允许在控件 Dispatcher 上读取。
        if (!Dispatcher.CheckAccess())
        {
            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                _ = Dispatcher.InvokeAsync(() => OnThemeApplied(sender, e), DispatcherPriority.DataBind);
            }

            return;
        }

        // 所有控件都重新渲染以更新应用级主题资源。
        RequestRender();
    }

    /// <summary>
    /// DataContext变化事件 - 数据绑定生效时触发
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {

        // 延迟一点等待绑定完成
        Dispatcher.InvokeAsync(() =>
        {
            var contentText = Content as string ?? string.Empty;

            var changed = !string.Equals(contentText, _latestText, StringComparison.Ordinal);
            _latestText = contentText;
            if (changed || MarkdownDocument?.Document == null)
                RequestRender();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 布局更新事件 - 检测绑定延迟完成
    /// </summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // 只检查一次，避免无限循环
        if (_hasCheckedBindingAfterLoad) return;

        _hasCheckedBindingAfterLoad = true;

        var contentText = Content as string ?? string.Empty;
        // 检测绑定延迟，包括绑定到空字符串的情况。
        var changed = !string.Equals(contentText, _latestText, StringComparison.Ordinal);
        _latestText = contentText;
        if (changed && MarkdownDocument != null)
            RequestRender();
    }

    /// <summary>
    /// 控件加载完成事件 - 初始化父级引用和确保渲染完成
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isActive = true;
        _hasCheckedBindingAfterLoad = false;
        SubscribeThemeAppliedEvent();

        var contentText = Content as string ?? string.Empty;
        _latestText = contentText;

        // 先冻结当前内容，再确保主题资源存在；主题服务若同步发出通知时也能使用正确文本。
        _themeService.EnsureThemeApplied();

        // 查找并缓存父级 ScrollViewer
        _cachedParentScrollViewer = FindParentScrollViewer(this);

        // 每次重新加载都提交一次快照，覆盖卸载期间发生的属性变化和模板重建。
        RequestRender();
        // 确保在列表场景下内容已正确渲染
        if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
            && MarkdownDocument?.Document != null)
        {
            ConfigureFlowDocument(MarkdownDocument.Document);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isActive = false;
        _renderCoordinator.Cancel();
        _renderVersion++;
        _cachedParentScrollViewer = null;
        UnsubscribeThemeAppliedEvent();
    }

    /// <summary>
    /// 控件级别的鼠标滚轮事件处理 - 确保在任何外部容器配置下都能正确工作
    /// </summary>
    private void OnControlPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 如果禁用了垂直滚动条（列表场景），需要将滚轮事件转发给父级ScrollViewer
        if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            // 如果还没缓存，尝试查找父级ScrollViewer
            _cachedParentScrollViewer = FindParentScrollViewer(this);

            // 如果找到了父级ScrollViewer，手动触发滚动
            if (_cachedParentScrollViewer != null)
            {
                // 标记事件为已处理，防止内部控件再次处理
                e.Handled = true;

                // 计算滚动偏移量
                var offset = _cachedParentScrollViewer.VerticalOffset - e.Delta;
                _cachedParentScrollViewer.ScrollToVerticalOffset(offset);
            }
        }
        // 如果启用了滚动条，但控件本身能够接收事件，确保事件不会被吞掉
        // （这里不需要特殊处理，让事件继续传递给内部的 FlowDocumentScrollViewer）
    }

    /// <summary>
    /// FlowDocumentScrollViewer 的鼠标滚轮事件处理（保留用于兼容性）
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 这个方法现在主要由 OnControlPreviewMouseWheel 处理
        // 保留此方法是为了向后兼容，避免移除后可能的问题
    }

    private void SubscribeThemeAppliedEvent()
    {
        if (_isThemeAppliedSubscribed)
        {
            return;
        }

        _themeService.ThemeApplied += OnThemeApplied;
        _isThemeAppliedSubscribed = true;
    }

    private void UnsubscribeThemeAppliedEvent()
    {
        if (!_isThemeAppliedSubscribed)
        {
            return;
        }

        _themeService.ThemeApplied -= OnThemeApplied;
        _isThemeAppliedSubscribed = false;
    }

    /// <summary>
    /// 查找父级 ScrollViewer
    /// </summary>
    private ScrollViewer? FindParentScrollViewer(DependencyObject child)
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    /// <summary>
    /// 根据文档长度计算自适应防抖时间（更激进的策略防止卡顿）
    /// </summary>
    private int CalculateAdaptiveThrottle(int contentLength)
    {
        // 基础防抖时间
        var baseThrottle = StreamingThrottle;

        // 更激进的防抖策略，防止 AI 流式渲染时卡顿
        // 0-2KB: 使用基础防抖时间
        // 2KB-10KB: 线性增加到至少 300ms
        // 10KB-50KB: 线性增加到至少 600ms
        // 50KB+: 使用至少 1000ms

        if (contentLength < 2000)
        {
            return baseThrottle;
        }
        else if (contentLength < 10000)
        {
            var targetThrottle = Math.Max(baseThrottle, 300);
            var ratio = (contentLength - 2000) / 8000.0;
            return (int)Math.Round(baseThrottle + ratio * (targetThrottle - baseThrottle));
        }
        else if (contentLength < 50000)
        {
            var startThrottle = Math.Max(baseThrottle, 300);
            var targetThrottle = Math.Max(baseThrottle, 600);
            var ratio = (contentLength - 10000) / 40000.0;
            return (int)Math.Round(startThrottle + ratio * (targetThrottle - startThrottle));
        }
        else
        {
            return Math.Max(baseThrottle, 1000);
        }
    }

    #endregion
}
