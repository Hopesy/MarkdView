using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using MarkdView.Services.Theme;
using MarkdView.Renderers;
using MarkdView.ViewModels;

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

        if (!_isUpdatingFromViewModel && _internalViewModel != null)
        {
            // 同步到内部ViewModel
            _internalViewModel.Markdown = markdownText;

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
                _updateTimer.Start();
            }
            else
            {
                // 立即渲染
                RenderMarkdown();
            }
        }
    }
    #region 依赖属性

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownViewer),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

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
            new PropertyMetadata(ThemeMode.Dark, OnThemeChanged));

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
            new PropertyMetadata(14.0, OnFontSizeChanged));

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MarkdownViewModel),
            typeof(MarkdownViewer),
            new PropertyMetadata(null, OnViewModelChanged));

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

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

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
    /// ViewModel - 可以从外部注入或使用内部默认的
    /// </summary>
    public MarkdownViewModel ViewModel
    {
        get => (MarkdownViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
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

    // 服务
    private readonly MarkdownRenderer _renderingService;

    // ViewModel
    private MarkdownViewModel _internalViewModel;
    private bool _isUpdatingFromViewModel;

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

        // 初始化内部 ViewModel（先不赋值给ViewModel属性，避免触发回调）
        _internalViewModel = new MarkdownViewModel();

        // 配置 Markdig 管道
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseTaskLists()
            .UseMediaLinks()
            .Build();

        // 初始化渲染服务
        _renderingService = new MarkdownRenderer(_pipeline);

        // 现在可以安全地设置ViewModel了（_renderingService已初始化）
        ViewModel = _internalViewModel;

        // 配置流式渲染定时器
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(StreamingThrottle)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // 订阅 ViewModel 事件
        SubscribeToViewModel(_internalViewModel);

        // 订阅 Loaded 事件，用于延迟初始化
        this.Loaded += OnLoaded;

        // 订阅 DataContextChanged 事件，用于检测数据绑定何时生效
        this.DataContextChanged += OnDataContextChanged;

        // 订阅 LayoutUpdated 事件，用于检测绑定完成
        this.LayoutUpdated += OnLayoutUpdated;

        // 应用默认主题
        ThemeManager.ApplyTheme(Theme);

        Console.WriteLine($"[MarkdownViewer] Constructor END");
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

            // 如果已经有Markdown内容，触发渲染
            if (!string.IsNullOrEmpty(Markdown))
            {
                _lastRenderedText = Markdown;
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

    #region ViewModel 事件订阅

    private void SubscribeToViewModel(MarkdownViewModel viewModel)
    {
        if (viewModel == null) return;

        viewModel.MarkdownChanged += OnViewModelMarkdownChanged;
        viewModel.ThemeChanged += OnViewModelThemeChanged;
        viewModel.RenderingSettingsChanged += OnViewModelRenderingSettingsChanged;
    }

    private void UnsubscribeFromViewModel(MarkdownViewModel viewModel)
    {
        if (viewModel == null) return;

        viewModel.MarkdownChanged -= OnViewModelMarkdownChanged;
        viewModel.ThemeChanged -= OnViewModelThemeChanged;
        viewModel.RenderingSettingsChanged -= OnViewModelRenderingSettingsChanged;
    }

    private void OnViewModelMarkdownChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel) return;

        Console.WriteLine($"[MarkdownViewer] OnViewModelMarkdownChanged: newText length={_internalViewModel.Markdown.Length}");

        _isUpdatingFromViewModel = true;
        try
        {
            Markdown = _internalViewModel.Markdown;
            _lastRenderedText = _internalViewModel.Markdown;
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }

        // 在列表场景中（滚动条禁用），总是立即渲染，不使用流式渲染
        var isListScenario = VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;

        // 触发重新渲染
        if (EnableStreaming && !isListScenario)
        {
            _pendingText = _lastRenderedText;
            _hasPendingUpdate = true;
            _updateTimer.Stop();
            _updateTimer.Start();
        }
        else
        {
            RenderMarkdown();
        }
    }

    private void OnViewModelThemeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel) return;

        _isUpdatingFromViewModel = true;
        try
        {
            RenderMarkdown();
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }
    }

    private void OnViewModelRenderingSettingsChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel) return;

        _isUpdatingFromViewModel = true;
        try
        {
            RenderMarkdown();
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }
    }

    #endregion

    #region 依赖属性回调

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newText = e.NewValue as string ?? string.Empty;
        Console.WriteLine($"[MarkdownViewer] OnMarkdownChanged CALLED: newText length={newText.Length}, isViewer={d is MarkdownViewer}");

        if (d is MarkdownViewer viewer && !viewer._isUpdatingFromViewModel && viewer._internalViewModel != null)
        {
            // 调试输出：追踪Markdown属性变化
            Console.WriteLine($"[MarkdownViewer] OnMarkdownChanged PASSED: newText length={newText.Length}, MarkdownDocument={viewer.MarkdownDocument != null}");

            // 同步到 ViewModel
            viewer._internalViewModel.Markdown = newText;

            // 在列表场景中（滚动条禁用），总是立即渲染，不使用流式渲染
            // 使用依赖属性而不是MarkdownDocument的值，因为属性设置有顺序
            var isListScenario = viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;

            // 保存待渲染的文本
            viewer._lastRenderedText = newText;

            // 如果MarkdownDocument还没初始化，等待Loaded事件
            if (viewer.MarkdownDocument == null)
            {
                Console.WriteLine($"[MarkdownViewer] OnMarkdownChanged: MarkdownDocument is NULL, waiting for Loaded");
                return;
            }

            if (viewer.EnableStreaming && !isListScenario)
            {
                // 流式渲染（仅在非列表场景）
                viewer._pendingText = newText;
                viewer._hasPendingUpdate = true;
                viewer._updateTimer.Stop();
                viewer._updateTimer.Start();
            }
            else
            {
                // 立即渲染
                viewer.RenderMarkdown();
            }
        }
        else if (d is MarkdownViewer viewer2)
        {
            Console.WriteLine($"[MarkdownViewer] OnMarkdownChanged BLOCKED: isUpdatingFromViewModel={viewer2._isUpdatingFromViewModel}, hasViewModel={viewer2._internalViewModel != null}");
        }
    }

    private static void OnEnableStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer._internalViewModel != null)
        {
            viewer._internalViewModel.EnableStreaming = (bool)e.NewValue;
        }
    }

    private static void OnStreamingThrottleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer._internalViewModel != null)
        {
            var throttle = (int)e.NewValue;
            viewer._internalViewModel.StreamingThrottle = throttle;
            viewer._updateTimer.Interval = TimeSpan.FromMilliseconds(throttle);
        }
    }

    private static void OnEnableSyntaxHighlightingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer._internalViewModel != null)
        {
            viewer._internalViewModel.EnableSyntaxHighlighting = (bool)e.NewValue;
            // RenderingSettingsChanged 事件会触发重新渲染
        }
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && !viewer._isUpdatingFromViewModel && viewer._internalViewModel != null)
        {
            var newTheme = (ThemeMode)e.NewValue;
            viewer._internalViewModel.Theme = newTheme;

            // 应用主题到全局资源
            ThemeManager.ApplyTheme(newTheme);
        }
    }

    private static void OnFontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer._internalViewModel != null)
        {
            viewer._internalViewModel.FontFamily = (FontFamily)e.NewValue;
            // RenderingSettingsChanged 事件会触发重新渲染
        }
    }

    private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && viewer._internalViewModel != null)
        {
            viewer._internalViewModel.FontSize = (double)e.NewValue;
            // RenderingSettingsChanged 事件会触发重新渲染
        }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            // 取消订阅旧 ViewModel
            if (e.OldValue is MarkdownViewModel oldViewModel)
            {
                viewer.UnsubscribeFromViewModel(oldViewModel);
            }

            // 订阅新 ViewModel
            if (e.NewValue is MarkdownViewModel newViewModel)
            {
                viewer._internalViewModel = newViewModel;
                viewer.SubscribeToViewModel(newViewModel);

                // 同步状态
                viewer.SyncFromViewModel();
            }
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
        // 确保所有必需的服务和控件已初始化
        if (_renderingService == null || _internalViewModel == null || MarkdownDocument == null)
        {
            Console.WriteLine($"[MarkdownViewer] RenderMarkdown SKIPPED: renderingService={_renderingService != null}, viewModel={_internalViewModel != null}, document={MarkdownDocument != null}");
            return;
        }

        Console.WriteLine($"[MarkdownViewer] RenderMarkdown: text length={_lastRenderedText.Length}, scrollBarVisibility={MarkdownDocument.VerticalScrollBarVisibility}");

        try
        {
            // 创建代码块渲染器
            var codeBlockRenderer = new Renderers.CodeBlockRenderer(
                _internalViewModel.EnableSyntaxHighlighting,
                _internalViewModel.Theme);

            // 使用渲染服务转换 Markdown
            var flowDocument = _renderingService.ConvertMarkdownToFlowDocument(
                _lastRenderedText,
                _internalViewModel.FontFamily,
                _internalViewModel.FontSize,
                _internalViewModel.EnableSyntaxHighlighting,
                codeBlockRenderer);

            Console.WriteLine($"[MarkdownViewer] FlowDocument created with {flowDocument.Blocks.Count} blocks");

            // 先设置Document
            MarkdownDocument.Document = flowDocument;

            // 然后立即配置FlowDocument的页面属性（必须在设置Document之后）
            ConfigureFlowDocument(flowDocument);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MarkdownViewer] RenderMarkdown ERROR: {ex.Message}");

            // 渲染错误时显示错误信息
            var errorDocument = new FlowDocument();
            var errorParagraph = new Paragraph(new Run($"Markdown 渲染错误: {ex.Message}"))
            {
                Foreground = Brushes.Red
            };
            errorDocument.Blocks.Add(errorParagraph);

            // 先设置Document
            MarkdownDocument.Document = errorDocument;

            // 然后配置错误文档
            ConfigureFlowDocument(errorDocument);
        }
    }

    /// <summary>
    /// 配置 FlowDocument 的页面属性，以支持列表场景
    /// </summary>
    private void ConfigureFlowDocument(FlowDocument document)
    {
        if (document == null) return;

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

    /// <summary>
    /// 从 ViewModel 同步状态到依赖属性
    /// </summary>
    private void SyncFromViewModel()
    {
        _isUpdatingFromViewModel = true;
        try
        {
            Markdown = _internalViewModel.Markdown;
            Theme = _internalViewModel.Theme;
            EnableSyntaxHighlighting = _internalViewModel.EnableSyntaxHighlighting;
            EnableStreaming = _internalViewModel.EnableStreaming;
            StreamingThrottle = _internalViewModel.StreamingThrottle;
            FontFamily = _internalViewModel.FontFamily;
            FontSize = _internalViewModel.FontSize;

            // 直接更新渲染文本，因为 OnMarkdownChanged 被 _isUpdatingFromViewModel 跳过了
            _lastRenderedText = _internalViewModel.Markdown;
        }
        finally
        {
            _isUpdatingFromViewModel = false;
        }

        RenderMarkdown();
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// DataContext变化事件 - 数据绑定生效时触发
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Console.WriteLine($"[MarkdownViewer] OnDataContextChanged: DataContext={DataContext != null}");

        // 延迟一点等待绑定完成
        Dispatcher.InvokeAsync(() =>
        {
            Console.WriteLine($"[MarkdownViewer] OnDataContextChanged (delayed): Markdown length={Markdown?.Length ?? 0}");

            // 如果Markdown有值但还没渲染，强制渲染
            if (!string.IsNullOrEmpty(Markdown) && MarkdownDocument?.Document == null)
            {
                Console.WriteLine($"[MarkdownViewer] OnDataContextChanged: Forcing render");
                _lastRenderedText = Markdown;
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

        // 如果有Markdown内容但还没渲染，强制渲染
        if (!string.IsNullOrEmpty(Markdown) && MarkdownDocument?.Document == null)
        {
            Console.WriteLine($"[MarkdownViewer] OnLayoutUpdated: Detected Markdown length={Markdown.Length}, forcing render");
            _hasCheckedBindingAfterLoad = true;
            _lastRenderedText = Markdown;
            RenderMarkdown();
        }
    }

    /// <summary>
    /// 控件加载完成事件 - 初始化父级引用和确保渲染完成
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Console.WriteLine($"[MarkdownViewer] OnLoaded: Markdown length={Markdown?.Length ?? 0}, _lastRenderedText length={_lastRenderedText.Length}, Document={MarkdownDocument?.Document != null}");

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
    /// 处理鼠标滚轮事件 - 将事件冒泡到父级容器
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 如果禁用了垂直滚动条，需要手动将滚轮事件转发给父级ScrollViewer
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
                // 标记事件为已处理，防止FlowDocumentScrollViewer处理它
                e.Handled = true;

                // 计算滚动偏移量
                var offset = _cachedParentScrollViewer.VerticalOffset - e.Delta;
                _cachedParentScrollViewer.ScrollToVerticalOffset(offset);
            }
        }
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

    #endregion
}
