using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using MarkdView.Enums;
using MarkdView.Renderers;

namespace MarkdView.Controls;

/// <summary>
/// MarkdView - 现代化 WPF Markdown 渲染控件
/// 基于 MVVM 架构，支持流式渲染、语法高亮、主题切换
/// </summary>
public class MarkdownViewer : ContentControl
{
    static MarkdownViewer()
    {
        Console.WriteLine($"[MarkdownViewer] Static constructor called");
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MarkdownViewer),
            new FrameworkPropertyMetadata(typeof(MarkdownViewer)));
        Console.WriteLine($"[MarkdownViewer] DefaultStyleKey set to: {typeof(MarkdownViewer)}");

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
        Console.WriteLine($"[MarkdownViewer] Template set in code");
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        var markdownText = newContent as string ?? string.Empty;
        Console.WriteLine($"[MarkdownViewer] OnContentChanged: newText length={markdownText.Length}");

        // 保存待渲染的文本
        _lastRenderedText = markdownText;

        // 如果MarkdownDocument还没初始化，等待OnApplyTemplate
        if (MarkdownDocument == null)
        {
            Console.WriteLine($"[MarkdownViewer] OnContentChanged: MarkdownDocument is NULL, waiting");
            return;
        }

        // 在列表场景中（滚动条禁用），总是立即渲染
        var isListScenario = VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;

        if (EnableStreaming && !isListScenario)
        {
            // 流式渲染（仅在非列表场景）
            _pendingText = markdownText;
            _hasPendingUpdate = true;
            _updateTimer.Stop();

            // 自适应防抖：根据文档长度动态调整防抖时间
            var adaptiveThrottle = CalculateAdaptiveThrottle(markdownText.Length);
            _updateTimer.Interval = TimeSpan.FromMilliseconds(adaptiveThrottle);

            _updateTimer.Start();
        }
        else
        {
            // 立即渲染
            RenderMarkdown();
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
            new PropertyMetadata(50, OnStreamingThrottleChanged));

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
            new PropertyMetadata(ThemeMode.Auto, OnThemeChanged));

    public new static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(
            nameof(FontFamily),
            typeof(FontFamily),
            typeof(MarkdownViewer),
            new PropertyMetadata(new FontFamily("Microsoft YaHei UI, Segoe UI"), OnFontFamilyChanged));

    public new static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(
            nameof(FontSize),
            typeof(double),
            typeof(MarkdownViewer),
            new PropertyMetadata(12.0, OnFontSizeChanged));

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

    #endregion

    #region 公共属性

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

    #endregion

    #region 私有字段

    // Markdig 管道
    private readonly MarkdownPipeline _pipeline;

    // 流式渲染
    private readonly DispatcherTimer _updateTimer;
    private string _lastRenderedText = string.Empty;
    private string _pendingText = string.Empty;
    private bool _hasPendingUpdate;
    private DateTime _lastRenderTime = DateTime.MinValue;
    private const int MinRenderIntervalMs = 300; // 最小渲染间隔 300ms（防止卡顿）
    private int _skipFrameCount = 0; // 跳帧计数器
    private bool _isRendering = false; // 防止重入渲染标志

    // 服务
    private readonly MarkdownRenderer _renderingService;

    // 模板元素
    private FlowDocumentScrollViewer? _markdownDocument;
    private FlowDocumentScrollViewer? MarkdownDocument
    {
        get => _markdownDocument;
        set
        {
            Console.WriteLine($"[MarkdownViewer] MarkdownDocument setter: old={_markdownDocument != null}, new={value != null}, Stack={Environment.StackTrace.Split('\n')[1].Trim()}");
            _markdownDocument = value;
        }
    }

    // 缓存父级ScrollViewer，用于滚轮事件处理
    private ScrollViewer? _cachedParentScrollViewer;
    private bool _hasSearchedForParent;

    // 用于检测绑定延迟的标志
    private bool _hasCheckedBindingAfterLoad;

    #endregion

    #region 构造函数

    public MarkdownViewer()
    {
        Console.WriteLine($"[MarkdownViewer] Constructor START");

        // 配置 Markdig 管道
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseTaskLists()
            .UseMediaLinks()
            .Build();

        // 初始化渲染服务
        _renderingService = new MarkdownRenderer(_pipeline);

        // 配置流式渲染定时器
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(StreamingThrottle)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // 订阅 Loaded 事件，用于延迟初始化
        this.Loaded += OnLoaded;

        // 订阅 DataContextChanged 事件，用于检测数据绑定何时生效
        this.DataContextChanged += OnDataContextChanged;

        // 订阅 LayoutUpdated 事件，用于检测绑定完成
        this.LayoutUpdated += OnLayoutUpdated;

        // 订阅主题应用完成事件
        ThemeManager.ThemeApplied += OnThemeApplied;

        // 在 MarkdownViewer 级别处理鼠标滚轮事件，确保即使外部容器透明也能工作
        this.PreviewMouseWheel += OnControlPreviewMouseWheel;

        // 主题会通过 OnThemeChanged 回调自动应用，这里不需要手动调用
        Console.WriteLine($"[MarkdownViewer] Constructor END, Theme={Theme}");
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Console.WriteLine($"[MarkdownViewer] OnApplyTemplate START");

        // 从模板中获取 FlowDocumentScrollViewer
        if (GetTemplateChild("PART_MarkdownDocument") is FlowDocumentScrollViewer documentViewer)
        {
            MarkdownDocument = documentViewer;
            Console.WriteLine($"[MarkdownViewer] OnApplyTemplate: MarkdownDocument found");

            // 处理滚轮事件冒泡
            MarkdownDocument.PreviewMouseWheel += OnPreviewMouseWheel;

            // 同步滚动条可见性
            MarkdownDocument.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
            MarkdownDocument.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;

            // 如果已经有Content内容，触发渲染
            var contentText = Content as string ?? string.Empty;
            if (!string.IsNullOrEmpty(contentText))
            {
                _lastRenderedText = contentText;
                RenderMarkdown();
            }
        }
        else
        {
            Console.WriteLine($"[MarkdownViewer] OnApplyTemplate: MarkdownDocument NOT found");
        }

        Console.WriteLine($"[MarkdownViewer] OnApplyTemplate END");
    }

    #endregion

    #region 依赖属性回调

    private static void OnEnableStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // EnableStreaming 变化时不需要重新渲染，只影响后续更新行为
    }

    private static void OnStreamingThrottleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            var throttle = (int)e.NewValue;
            viewer._updateTimer.Interval = TimeSpan.FromMilliseconds(throttle);
        }
    }

    private static void OnEnableSyntaxHighlightingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 语法高亮设置变化，触发重新渲染
            viewer.RenderMarkdown();
        }
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            var newTheme = (ThemeMode)e.NewValue;
            Console.WriteLine($"[MarkdownViewer] OnThemeChanged: {e.OldValue} -> {newTheme}");

            if (newTheme == ThemeMode.Auto)
            {
                // Auto 模式：跟随全局主题
                Console.WriteLine($"[MarkdownViewer] Theme=Auto, using ThemeManager.CurrentTheme={ThemeManager.CurrentTheme}");
                ThemeManager.ApplyTheme(ThemeManager.CurrentTheme);
            }
            else
            {
                // 显式设置主题：同步到全局并应用
                Console.WriteLine($"[MarkdownViewer] Theme explicitly set to {newTheme}, syncing to ThemeManager");
                ThemeManager.ApplyTheme(newTheme);
            }
        }
    }

    private static void OnFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 字体变化，触发重新渲染
            viewer.RenderMarkdown();
        }
    }

    private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 字号变化，触发重新渲染
            viewer.RenderMarkdown();
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

                // 更新FlowDocument的页面设置以适应内容
                if (viewer.MarkdownDocument.Document != null)
                {
                    viewer.ConfigureFlowDocument(viewer.MarkdownDocument.Document);
                }
            }
            else
            {
                viewer.MarkdownDocument.VerticalAlignment = VerticalAlignment.Stretch;
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

    #endregion

    #region Markdown 渲染

    /// <summary>
    /// 流式渲染定时器回调
    /// </summary>
    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        _updateTimer.Stop();

        if (_hasPendingUpdate)
        {
            // 检查距离上次渲染的时间间隔
            var timeSinceLastRender = (DateTime.Now - _lastRenderTime).TotalMilliseconds;

            if (timeSinceLastRender < MinRenderIntervalMs)
            {
                // 如果距离上次渲染时间太短，延迟渲染
                var remainingDelay = (int)(MinRenderIntervalMs - timeSinceLastRender);
                _updateTimer.Interval = TimeSpan.FromMilliseconds(remainingDelay);
                _updateTimer.Start();

                _skipFrameCount++;
                if (_skipFrameCount % 5 == 0)
                {
                    Console.WriteLine($"[MarkdownViewer] 跳帧保护: 已跳过 {_skipFrameCount} 帧，等待 {remainingDelay}ms");
                }
                return;
            }

            // 重置跳帧计数
            if (_skipFrameCount > 0)
            {
                Console.WriteLine($"[MarkdownViewer] 跳帧结束: 共跳过 {_skipFrameCount} 帧");
                _skipFrameCount = 0;
            }

            _lastRenderedText = _pendingText;
            _hasPendingUpdate = false;
            RenderMarkdown();
        }
    }

    /// <summary>
    /// 渲染 Markdown 文本为 FlowDocument
    /// </summary>
    private void RenderMarkdown()
    {
        // 防止重入渲染（避免死锁和无限循环）
        if (_isRendering)
        {
            Console.WriteLine($"[MarkdownViewer] RenderMarkdown SKIPPED: 渲染正在进行中，防止重入");
            return;
        }

        // 确保所有必需的服务和控件已初始化
        if (_renderingService == null || MarkdownDocument == null)
        {
            Console.WriteLine($"[MarkdownViewer] RenderMarkdown SKIPPED: renderingService={_renderingService != null}, document={MarkdownDocument != null}");
            return;
        }

        _isRendering = true;
        Console.WriteLine($"[MarkdownViewer] RenderMarkdown START: text length={_lastRenderedText.Length}");

        // 记录渲染开始时间
        var renderStartTime = DateTime.Now;

        // 捕获需要的变量（避免闭包问题）
        var textToRender = _lastRenderedText;
        var fontFamily = FontFamily;
        var fontSize = FontSize;
        var enableHighlighting = EnableSyntaxHighlighting;
        // 如果 Theme=Auto，使用全局主题；否则使用控件自己的主题设置
        var theme = Theme == ThemeMode.Auto ? ThemeManager.CurrentTheme : Theme;

        // 使用低优先级异步渲染，保持 UI 响应
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (MarkdownDocument == null)
                {
                    _isRendering = false;
                    return;
                }

                // 创建代码块渲染器
                var codeBlockRenderer = new Renderers.CodeBlockRenderer(
                    enableHighlighting,
                    theme,
                    fontSize);

                // 在 UI 线程解析和创建 FlowDocument（但使用低优先级，让 UI 保持响应）
                var flowDocument = _renderingService.ConvertMarkdownToFlowDocument(
                    textToRender,
                    fontFamily,
                    fontSize,
                    enableHighlighting,
                    codeBlockRenderer);

                Console.WriteLine($"[MarkdownViewer] FlowDocument created with {flowDocument.Blocks.Count} blocks");

                // 设置Document
                MarkdownDocument.Document = flowDocument;

                // 配置FlowDocument的页面属性
                ConfigureFlowDocument(flowDocument);

                // 记录渲染完成时间
                _lastRenderTime = renderStartTime;
                var totalTime = (DateTime.Now - renderStartTime).TotalMilliseconds;
                Console.WriteLine($"[MarkdownViewer] Render completed in {totalTime:F1}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MarkdownViewer] Render ERROR: {ex.Message}");
                ShowErrorDocument(ex.Message);
            }
            finally
            {
                _isRendering = false;
            }
        }, System.Windows.Threading.DispatcherPriority.Background); // 使用 Background 优先级，保持 UI 响应
    }

    private void ShowErrorDocument(string errorMessage)
    {
        if (MarkdownDocument == null) return;

        try
        {
            Console.WriteLine($"[MarkdownViewer] ShowErrorDocument: {errorMessage}");

            // 渲染错误时显示错误信息
            var errorDocument = new FlowDocument();
            var errorParagraph = new Paragraph(new Run($"Markdown 渲染错误: {errorMessage}"))
            {
                Foreground = Brushes.Red
            };
            errorDocument.Blocks.Add(errorParagraph);

            // 设置Document
            MarkdownDocument.Document = errorDocument;

            // 配置错误文档
            ConfigureFlowDocument(errorDocument);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MarkdownViewer] ShowErrorDocument ERROR: {ex.Message}");
        }
    }

    /// <summary>
    /// 配置 FlowDocument 的页面属性，以支持列表场景
    /// </summary>
    private void ConfigureFlowDocument(FlowDocument document)
    {
        if (document == null || MarkdownDocument == null) return;

        // 如果禁用了垂直滚动条（列表场景），设置页面为自动高度
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
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 主题应用完成事件 - ThemeManager 替换资源字典后触发
    /// </summary>
    private void OnThemeApplied(object? sender, EventArgs e)
    {
        Console.WriteLine($"[MarkdownViewer] OnThemeApplied: Theme={Theme}, ThemeManager.CurrentTheme={ThemeManager.CurrentTheme}");

        // 如果控件设置为 Auto 模式，需要重新渲染以应用新的全局主题
        if (Theme == ThemeMode.Auto)
        {
            Console.WriteLine($"[MarkdownViewer] Theme=Auto, re-rendering with global theme");
        }

        // 无论哪种模式，都需要重新渲染以更新主题相关的样式
        RenderMarkdown();
    }

    /// <summary>
    /// DataContext变化事件 - 数据绑定生效时触发
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Console.WriteLine($"[MarkdownViewer] OnDataContextChanged: DataContext={DataContext != null}");

        // 延迟一点等待绑定完成
        Dispatcher.InvokeAsync(() =>
        {
            var contentText = Content as string ?? string.Empty;
            Console.WriteLine($"[MarkdownViewer] OnDataContextChanged (delayed): Markdown length={contentText.Length}");

            // 如果Content有值但还没渲染，强制渲染
            if (!string.IsNullOrEmpty(contentText) && MarkdownDocument?.Document == null)
            {
                Console.WriteLine($"[MarkdownViewer] OnDataContextChanged: Forcing render");
                _lastRenderedText = contentText;
                RenderMarkdown();
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 布局更新事件 - 检测绑定延迟完成
    /// </summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // 只检查一次，避免无限循环
        if (_hasCheckedBindingAfterLoad) return;

        var contentText = Content as string ?? string.Empty;
        // 如果有Content内容但还没渲染，强制渲染
        if (!string.IsNullOrEmpty(contentText) && MarkdownDocument?.Document == null)
        {
            Console.WriteLine($"[MarkdownViewer] OnLayoutUpdated: Detected Markdown length={contentText.Length}, forcing render");
            _hasCheckedBindingAfterLoad = true;
            _lastRenderedText = contentText;
            RenderMarkdown();
        }
    }

    /// <summary>
    /// 控件加载完成事件 - 初始化父级引用和确保渲染完成
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var contentText = Content as string ?? string.Empty;
        Console.WriteLine($"[MarkdownViewer] OnLoaded: Markdown length={contentText.Length}, _lastRenderedText length={_lastRenderedText.Length}, Document={MarkdownDocument?.Document != null}");

        // 查找并缓存父级 ScrollViewer
        if (!_hasSearchedForParent)
        {
            _cachedParentScrollViewer = FindParentScrollViewer(this);
            _hasSearchedForParent = true;
            Console.WriteLine($"[MarkdownViewer] OnLoaded: Parent ScrollViewer found={_cachedParentScrollViewer != null}");
        }

        // 如果有待渲染的文本但还没有文档，立即渲染
        if (!string.IsNullOrEmpty(_lastRenderedText) && MarkdownDocument?.Document == null)
        {
            Console.WriteLine($"[MarkdownViewer] OnLoaded: Forcing immediate render for text length={_lastRenderedText.Length}");
            RenderMarkdown();
        }
        // 确保在列表场景下内容已正确渲染
        else if (VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            // 如果有文档，强制重新配置
            if (MarkdownDocument?.Document != null)
            {
                ConfigureFlowDocument(MarkdownDocument.Document);
            }
        }
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
            if (!_hasSearchedForParent)
            {
                _cachedParentScrollViewer = FindParentScrollViewer(this);
                _hasSearchedForParent = true;
            }

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
        // 0-2KB: 使用基础防抖时间（50ms）
        // 2KB-10KB: 线性增加到 300ms
        // 10KB-50KB: 线性增加到 600ms
        // 50KB+: 使用 1000ms

        if (contentLength < 2000)
        {
            return baseThrottle;
        }
        else if (contentLength < 10000)
        {
            // 2KB-10KB: 50ms -> 300ms
            var ratio = (contentLength - 2000) / 8000.0;
            return (int)(baseThrottle + ratio * (300 - baseThrottle));
        }
        else if (contentLength < 50000)
        {
            // 10KB-50KB: 300ms -> 600ms
            var ratio = (contentLength - 10000) / 40000.0;
            return (int)(300 + ratio * 300);
        }
        else
        {
            // 50KB+: 1000ms
            return 1000;
        }
    }

    #endregion
}
