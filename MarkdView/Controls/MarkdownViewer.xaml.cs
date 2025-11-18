using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using MarkdView.Services.Theme;
using MarkdView.Services.Renderers;
using MarkdView.ViewModels;

namespace MarkdView.Controls;

/// <summary>
/// MarkdView - 现代化 WPF Markdown 渲染控件
/// 基于 MVVM 架构，支持流式渲染、语法高亮、主题切换
/// </summary>
public partial class MarkdownViewer : UserControl
{
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
    private readonly RenderingService _renderingService;

    // ViewModel
    private MarkdownViewModel _internalViewModel;
    private bool _isUpdatingFromViewModel;

    #endregion

    #region 构造函数

    public MarkdownViewer()
    {
        InitializeComponent();

        // 初始化内部 ViewModel
        _internalViewModel = new MarkdownViewModel();
        ViewModel = _internalViewModel;

        // 配置 Markdig 管道
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseTaskLists()
            .UseMediaLinks()
            .Build();

        // 初始化渲染服务
        _renderingService = new RenderingService(_pipeline);

        // 配置流式渲染定时器
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(StreamingThrottle)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // 处理滚轮事件冒泡
        MarkdownDocument.PreviewMouseWheel += OnPreviewMouseWheel;

        // 订阅 ViewModel 事件
        SubscribeToViewModel(_internalViewModel);

        // 应用默认主题
        var themeService = new ThemeService();
        themeService.ApplyTheme(Theme);

        // 初始渲染
        RenderMarkdown();
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

        // 触发重新渲染
        if (EnableStreaming)
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
        if (d is MarkdownViewer viewer && !viewer._isUpdatingFromViewModel && viewer._internalViewModel != null)
        {
            var newText = e.NewValue as string ?? string.Empty;

            // 同步到 ViewModel
            viewer._internalViewModel.Markdown = newText;

            if (viewer.EnableStreaming)
            {
                // 流式渲染
                viewer._pendingText = newText;
                viewer._hasPendingUpdate = true;
                viewer._updateTimer.Stop();
                viewer._updateTimer.Start();
            }
            else
            {
                // 立即渲染
                viewer._lastRenderedText = newText;
                viewer.RenderMarkdown();
            }
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
            var themeService = new ThemeService();
            themeService.ApplyTheme(newTheme);
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
            return;
        }

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

            MarkdownDocument.Document = flowDocument;
        }
        catch (Exception ex)
        {
            // 渲染错误时显示错误信息
            var errorDocument = new FlowDocument();
            var errorParagraph = new Paragraph(new Run($"Markdown 渲染错误: {ex.Message}"))
            {
                Foreground = Brushes.Red
            };
            errorDocument.Blocks.Add(errorParagraph);
            MarkdownDocument.Document = errorDocument;
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
    /// 处理鼠标滚轮事件 - 将事件冒泡到父级容器
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (MarkdownDocument.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            if (e.Handled)
            {
                return;
            }

            var scrollViewer = FindParentScrollViewer(this);
            if (scrollViewer != null)
            {
                var offset = scrollViewer.VerticalOffset - (e.Delta / 3.0);
                scrollViewer.ScrollToVerticalOffset(offset);
                e.Handled = true;
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
