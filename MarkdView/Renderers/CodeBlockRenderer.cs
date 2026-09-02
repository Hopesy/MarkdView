using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MarkdView.Interactions;
using MarkdView.Services;
using MarkdView.Enums;

namespace MarkdView.Renderers;

/// <summary>
/// 代码块UI渲染器 - 负责构建代码块的所有UI元素
/// </summary>
public class CodeBlockRenderer
{
    private readonly bool _enableSyntaxHighlighting;
    private readonly ThemeMode _themeMode;
    private readonly double _baseFontSize;
    private readonly IClipboardService _clipboardService;
    private readonly ISyntaxHighlighter _syntaxHighlighter;

    /// <summary>
    /// 保留早期版本的三参数构造函数签名。
    /// </summary>
    public CodeBlockRenderer(
        bool enableSyntaxHighlighting,
        ThemeMode themeMode = ThemeMode.Dark,
        double baseFontSize = 12.0)
        : this(enableSyntaxHighlighting, themeMode, baseFontSize, null, null)
    {
    }

    public CodeBlockRenderer(
        bool enableSyntaxHighlighting,
        ThemeMode themeMode,
        double baseFontSize,
        IClipboardService? clipboardService = null,
        ISyntaxHighlighter? syntaxHighlighter = null)
    {
        if (double.IsNaN(baseFontSize)
            || double.IsInfinity(baseFontSize)
            || baseFontSize <= 0
            || baseFontSize > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(baseFontSize), baseFontSize, "字号必须大于 0 且不超过 200。");
        }

        if (themeMode is not (ThemeMode.Auto or ThemeMode.Light or ThemeMode.Dark))
        {
            throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "未知主题模式。");
        }

        _enableSyntaxHighlighting = enableSyntaxHighlighting;
        _themeMode = themeMode == ThemeMode.Auto ? ThemeMode.Dark : themeMode;
        _baseFontSize = baseFontSize;
        _clipboardService = clipboardService ?? new WpfClipboardService();
        _syntaxHighlighter = syntaxHighlighter ?? new DefaultSyntaxHighlighter();
    }

    /// <summary>
    /// 渲染代码块为 BlockUIContainer
    /// </summary>
    public BlockUIContainer Render(string code, string? language)
    {
        var codeContainer = new Grid
        {
            Margin = GetThicknessResource("Markdown.CodeBlock.Margin", MarkdownLayoutDefaults.CodeBlockMargin),
            SnapsToDevicePixels = true
        };

        var mainBorder = new Border
        {
            BorderThickness = GetThicknessResource("Markdown.CodeBlock.BorderThickness", MarkdownLayoutDefaults.TableBorderThickness),
            CornerRadius = GetCornerRadiusResource("Markdown.CodeBlock.CornerRadius", MarkdownLayoutDefaults.CodeBlockCornerRadius),
            SnapsToDevicePixels = true,
            ClipToBounds = true
        };

        // 使用动态资源绑定，支持主题切换
        SetDynamicResource(mainBorder, Border.BackgroundProperty,
            "Markdown.CodeBlock.Background",
            new SolidColorBrush(FallbackColor(Color.FromRgb(0x28, 0x2C, 0x34), Color.FromRgb(0xF1, 0xF5, 0xF9))));
        SetDynamicResource(mainBorder, Border.BorderBrushProperty,
            "Markdown.CodeBlock.Border",
            new SolidColorBrush(FallbackColor(Color.FromRgb(0x21, 0x25, 0x2B), Color.FromRgb(0xE8, 0xEE, 0xF6))));

        var containerGrid = new Grid
        {
            ClipToBounds = true
        };
        containerGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(GetDoubleResource("Markdown.CodeBlock.Header.Height", 28))
        }); // 标题栏
        containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 代码内容

        // 创建标题栏
        var headerBorder = CreateHeader(code, language);
        Grid.SetRow(headerBorder, 0);
        containerGrid.Children.Add(headerBorder);

        // 创建代码内容区域
        var codeScrollViewer = CreateCodeContent(code, language);
        Grid.SetRow(codeScrollViewer, 1);
        containerGrid.Children.Add(codeScrollViewer);

        mainBorder.Child = containerGrid;
        codeContainer.Children.Add(mainBorder);

        return new BlockUIContainer(codeContainer);
    }

    /// <summary>
    /// 创建代码块标题栏
    /// </summary>
    private Border CreateHeader(string code, string? language)
    {
        var headerGrid = new Grid();
        var headerBorder = new Border
        {
            CornerRadius = GetCornerRadiusResource(
                "Markdown.CodeBlock.Header.CornerRadius",
                MarkdownLayoutDefaults.CodeBlockHeaderCornerRadius),
            SnapsToDevicePixels = true,
            ClipToBounds = true,
            Child = headerGrid
        };

        // 使用动态资源绑定标题栏背景色
        SetDynamicResource(headerBorder, Border.BackgroundProperty,
            "Markdown.CodeBlock.Header.Background",
                new SolidColorBrush(FallbackColor(Color.FromRgb(0x21, 0x25, 0x2B), Color.FromRgb(0xE8, 0xEE, 0xF6))));

        // 左侧：Mac 风格三个圆点
        var dotsPanel = CreateMacStyleDots();
        headerGrid.Children.Add(dotsPanel);

        // 中间：语言标签
        if (!string.IsNullOrEmpty(language))
        {
            var displayLanguage = NormalizeLanguage(language);
            var langLabel = new TextBlock
            {
                Text = displayLanguage,
                FontSize = _baseFontSize * GetDoubleResource(
                    "Markdown.CodeBlock.Language.FontScale",
                    0.9),
                FontWeight = FontWeights.Medium,
                Opacity = 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 使用动态资源绑定语言标签颜色
            SetDynamicResource(langLabel, TextBlock.ForegroundProperty,
                "Markdown.CodeBlock.Language.Foreground",
                new SolidColorBrush(FallbackColor(Color.FromRgb(0xAB, 0xB2, 0xBF), Color.FromRgb(0x47, 0x55, 0x69))));

            headerGrid.Children.Add(langLabel);
        }

        // 右侧：复制按钮
        var copyButton = CreateCopyButton(code);
        headerGrid.Children.Add(copyButton);

        return headerBorder;
    }

    /// <summary>
    /// 创建 Mac 风格的三个圆点
    /// </summary>
    private StackPanel CreateMacStyleDots()
    {
        var dotsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = GetThicknessResource(
                "Markdown.CodeBlock.DotPanel.Margin",
                new Thickness(8, 0, 0, 0)),
            Opacity = 0.92
        };

        // 红色圆点
        var dotSize = GetDoubleResource("Markdown.CodeBlock.Dot.Size", 8);
        var dotSpacing = GetDoubleResource("Markdown.CodeBlock.Dot.Spacing", 5);
        dotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = dotSize,
            Height = dotSize,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x5F, 0x56)),
            Margin = new Thickness(0, 0, dotSpacing, 0)
        });

        // 黄色圆点
        dotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = dotSize,
            Height = dotSize,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xBD, 0x2E)),
            Margin = new Thickness(0, 0, dotSpacing, 0)
        });

        // 绿色圆点
        dotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = dotSize,
            Height = dotSize,
            Fill = new SolidColorBrush(Color.FromRgb(0x27, 0xC9, 0x3F))
        });

        return dotsPanel;
    }

    /// <summary>
    /// 创建复制按钮
    /// </summary>
    private Button CreateCopyButton(string code)
    {
        var copyButton = new Button
        {
            Content = "⧉",
            ToolTip = "复制代码",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = GetThicknessResource(
                "Markdown.CodeBlock.CopyButton.Margin",
                new Thickness(0, 0, 6, 0)),
            Padding = new Thickness(0),
            Width = GetDoubleResource("Markdown.CodeBlock.CopyButton.Width", 22),
            Height = GetDoubleResource("Markdown.CodeBlock.CopyButton.Height", 22),
            BorderThickness = new Thickness(0),
            FontSize = _baseFontSize * 1.0,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.96,
            Cursor = Cursors.Hand,
            Tag = code,
            Template = CreateCopyButtonTemplate()
        };
        System.Windows.Automation.AutomationProperties.SetName(copyButton, "复制代码");

        // 使用动态资源绑定按钮颜色
        SetDynamicResource(copyButton, Button.BackgroundProperty,
            "Markdown.CodeBlock.CopyButton.Background",
            Brushes.Transparent);
        SetDynamicResource(copyButton, Button.ForegroundProperty,
            "Markdown.CodeBlock.CopyButton.Foreground",
            new SolidColorBrush(FallbackColor(Color.FromRgb(0xAB, 0xB2, 0xBF), Color.FromRgb(0x33, 0x41, 0x55))));
        SetDynamicResource(copyButton, Button.BorderBrushProperty,
            "Markdown.CodeBlock.CopyButton.Border",
            Brushes.Transparent);

        // 复制按钮点击事件
        copyButton.Click += (s, e) =>
        {
            if (s is Button btn && btn.Tag is string codeText)
            {
                try
                {
                    _clipboardService.SetText(codeText);
                    btn.Content = "✓";

                    // 2秒后恢复按钮文本
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (ts, te) =>
                    {
                        btn.Content = "⧉";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch
                {
                    btn.Content = "✕";
                }
            }
        };

        return copyButton;
    }

    /// <summary>
    /// 创建复制按钮的控件模板
    /// </summary>
    private ControlTemplate CreateCopyButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));

        var factory = new FrameworkElementFactory(typeof(Border));
        factory.Name = "border";
        factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        factory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        factory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
        factory.SetValue(
            Border.CornerRadiusProperty,
            GetCornerRadiusResource("Markdown.CodeBlock.CopyButton.CornerRadius", MarkdownLayoutDefaults.CopyButtonCornerRadius));
        factory.SetValue(Border.SnapsToDevicePixelsProperty, true);

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentPresenter);

        template.VisualTree = factory;

        // 添加鼠标悬停效果
        var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Button.BackgroundProperty,
            GetBrushResource("Markdown.CodeBlock.CopyButton.HoverBackground", Color.FromRgb(0x4C, 0x50, 0x58))));
        trigger.Setters.Add(new Setter(Button.BorderBrushProperty,
            GetBrushResource("Markdown.CodeBlock.CopyButton.HoverBorder", Color.FromRgb(0x6A, 0x70, 0x7B))));
        trigger.Setters.Add(new Setter(Button.OpacityProperty, 1.0));
        template.Triggers.Add(trigger);

        var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty,
            GetBrushResource("Markdown.CodeBlock.CopyButton.PressedBackground", Color.FromRgb(0x36, 0x3A, 0x43))));
        pressedTrigger.Setters.Add(new Setter(Button.OpacityProperty, 0.92));
        template.Triggers.Add(pressedTrigger);

        return template;
    }

    /// <summary>
    /// 创建代码内容区域
    /// </summary>
    private ScrollViewer CreateCodeContent(string code, string? language)
    {
        var codeScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = GetThicknessResource("Markdown.CodeBlock.Content.Padding", MarkdownLayoutDefaults.CodeContentPadding),
            MaxHeight = GetDoubleResource(
                "Markdown.CodeBlock.Content.MaxHeight",
                MarkdownLayoutDefaults.CodeContentMaxHeight)
        };

        // 修复滚轮滚动问题：手动将滚轮事件转发给父级或在内部处理
        codeScrollViewer.PreviewMouseWheel += (s, e) =>
        {
            if (s is not ScrollViewer scrollViewer) return;

            var delta = e.Delta;
            var offset = scrollViewer.VerticalOffset;
            var scrollableHeight = scrollViewer.ScrollableHeight;

            // 判断是否应该在代码块内部滚动
            bool shouldScrollInternally = false;

            if (scrollableHeight > 0)
            {
                if (delta > 0 && offset > 0)
                {
                    // 向上滚动且未到顶部
                    shouldScrollInternally = true;
                }
                else if (delta < 0 && offset < scrollableHeight)
                {
                    // 向下滚动且未到底部
                    shouldScrollInternally = true;
                }
            }

            if (shouldScrollInternally)
            {
                // 在代码块内部滚动
                scrollViewer.ScrollToVerticalOffset(offset - delta / 3.0);
                e.Handled = true;
            }
            else
            {
                // 需要将事件传递给父级，强制不处理事件
                e.Handled = false;

                // 手动触发父级的滚动 - 查找父级的 ScrollViewer
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(scrollViewer);
                while (parent != null)
                {
                    if (parent is ScrollViewer parentScrollViewer)
                    {
                        var parentOffset = parentScrollViewer.VerticalOffset;
                        parentScrollViewer.ScrollToVerticalOffset(parentOffset - delta / 3.0);
                        e.Handled = true;
                        break;
                    }
                    // FlowDocumentScrollViewer 内部也有 ScrollViewer，继续向上查找
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }
        };

        var codeTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap
        };

        // 设置代码块字体
        codeTextBlock.FontFamily = GetFontFamilyResource(
            "Markdown.CodeFontFamily",
            new FontFamily("Consolas, Monaco, Courier New, monospace"));
        // 代码内容字体大小由主题比例控制，避免不同主题出现额外垂直空白。
        codeTextBlock.FontSize = _baseFontSize * GetDoubleResource("Markdown.CodeBlock.CodeFontScale", 0.92);

        // 启用语法高亮
        if (_enableSyntaxHighlighting)
        {
            _syntaxHighlighter.ApplyHighlighting(codeTextBlock, code, language);
        }
        else
        {
            // 不启用高亮时使用动态资源绑定前景色
            SetDynamicResource(codeTextBlock, TextBlock.ForegroundProperty,
                "Markdown.CodeBlock.Foreground",
                new SolidColorBrush(FallbackColor(Color.FromRgb(0xAB, 0xB2, 0xBF), Color.FromRgb(0x1F, 0x29, 0x37))));
            codeTextBlock.Text = code;
        }

        codeScrollViewer.Content = codeTextBlock;
        return codeScrollViewer;
    }

    private static string NormalizeLanguage(string language)
    {
        var parts = language.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "CODE";
        }

        return parts[0].Trim().ToUpperInvariant();
    }

    /// <summary>
    /// 设置动态资源引用
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

    private static Thickness GetThicknessResource(string resourceKey, Thickness defaultValue)
        => Application.Current?.TryFindResource(resourceKey) is Thickness value ? value : defaultValue;

    private static double GetDoubleResource(string resourceKey, double defaultValue)
        => Application.Current?.TryFindResource(resourceKey) is double value
            && value > 0
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            ? value
            : defaultValue;

    private static CornerRadius GetCornerRadiusResource(string resourceKey, CornerRadius defaultValue)
        => Application.Current?.TryFindResource(resourceKey) is CornerRadius value
            ? value
            : defaultValue;

    private static FontFamily GetFontFamilyResource(string resourceKey, FontFamily defaultValue)
        => Application.Current?.TryFindResource(resourceKey) is FontFamily value ? value : defaultValue;

    private Brush GetBrushResource(string resourceKey, Color defaultColor)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(defaultColor);
    }

    private Color FallbackColor(Color dark, Color light)
        => _themeMode == ThemeMode.Light ? light : dark;
}
