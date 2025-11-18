using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MarkdView.Services.Theme;
using MarkdView.Services.SyntaxHighlight;
using MarkdView;

namespace MarkdView.Renderers;

/// <summary>
/// 代码块UI渲染器 - 负责构建代码块的所有UI元素
/// </summary>
public class CodeBlockRenderer
{
    private readonly bool _enableSyntaxHighlighting;
    private readonly ThemeMode _themeMode;

    public CodeBlockRenderer(bool enableSyntaxHighlighting, ThemeMode themeMode = ThemeMode.Dark)
    {
        _enableSyntaxHighlighting = enableSyntaxHighlighting;
        _themeMode = themeMode;
    }

    /// <summary>
    /// 渲染代码块为 BlockUIContainer
    /// </summary>
    public BlockUIContainer Render(string code, string? language)
    {
        var codeContainer = new Grid
        {
            Margin = new Thickness(0, 16, 0, 16)
        };

        var mainBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };

        // 使用动态资源绑定，支持主题切换
        SetDynamicResource(mainBorder, Border.BackgroundProperty,
            "Markdown.CodeBlock.Background",
            new SolidColorBrush(Color.FromRgb(0x28, 0x2C, 0x34)));
        SetDynamicResource(mainBorder, Border.BorderBrushProperty,
            "Markdown.CodeBlock.Border",
            new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x2B)));

        var containerGrid = new Grid();
        containerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // 标题栏
        containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 代码内容

        // 创建标题栏
        var headerGrid = CreateHeader(code, language);
        Grid.SetRow(headerGrid, 0);
        containerGrid.Children.Add(headerGrid);

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
    private Grid CreateHeader(string code, string? language)
    {
        var headerGrid = new Grid();

        // 使用动态资源绑定标题栏背景色
        SetDynamicResource(headerGrid, Grid.BackgroundProperty,
            "Markdown.CodeBlock.Header.Background",
            new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x2B)));

        // 左侧：Mac 风格三个圆点
        var dotsPanel = CreateMacStyleDots();
        headerGrid.Children.Add(dotsPanel);

        // 中间：语言标签
        if (!string.IsNullOrEmpty(language))
        {
            var langLabel = new TextBlock
            {
                Text = language,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 使用动态资源绑定语言标签颜色
            SetDynamicResource(langLabel, TextBlock.ForegroundProperty,
                "Markdown.CodeBlock.Language.Foreground",
                new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF)));

            headerGrid.Children.Add(langLabel);
        }

        // 右侧：复制按钮
        var copyButton = CreateCopyButton(code);
        headerGrid.Children.Add(copyButton);

        return headerGrid;
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
            Margin = new Thickness(12, 0, 0, 0)
        };

        // 红色圆点
        dotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x5F, 0x56)),
            Margin = new Thickness(0, 0, 8, 0)
        });

        // 黄色圆点
        dotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xBD, 0x2E)),
            Margin = new Thickness(0, 0, 8, 0)
        });

        // 绿色圆点
        dotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 12,
            Height = 12,
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
            Content = "复制",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Padding = new Thickness(12, 4, 12, 4),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Cursor = Cursors.Hand,
            Tag = code,
            Template = CreateCopyButtonTemplate()
        };

        // 使用动态资源绑定按钮颜色
        SetDynamicResource(copyButton, Button.BackgroundProperty,
            "Markdown.CodeBlock.CopyButton.Background",
            new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x48)));
        SetDynamicResource(copyButton, Button.ForegroundProperty,
            "Markdown.CodeBlock.CopyButton.Foreground",
            new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF)));

        // 复制按钮点击事件
        copyButton.Click += (s, e) =>
        {
            if (s is Button btn && btn.Tag is string codeText)
            {
                try
                {
                    Clipboard.SetText(codeText);
                    btn.Content = "已复制!";

                    // 2秒后恢复按钮文本
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (ts, te) =>
                    {
                        btn.Content = "复制";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch
                {
                    btn.Content = "复制失败";
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
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentPresenter);

        template.VisualTree = factory;

        // 添加鼠标悬停效果
        var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Button.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0x4C, 0x50, 0x58))));
        template.Triggers.Add(trigger);

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
            Padding = new Thickness(16, 12, 16, 12)
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

        // 使用动态资源绑定字体（也可以使用静态获取，字体通常不会频繁改变）
        SetDynamicResource(codeTextBlock, TextBlock.FontFamilyProperty,
            "Markdown.CodeFontFamily",
            new FontFamily("Consolas, Monaco, Courier New, monospace"));
        SetDynamicResource(codeTextBlock, TextBlock.FontSizeProperty,
            "Markdown.CodeFontSize",
            13.5);

        // 启用语法高亮
        if (_enableSyntaxHighlighting)
        {
            // 获取当前主题
            SyntaxHighlighter.ApplyHighlighting(codeTextBlock, code, language, ThemeManager.GetCurrentTheme() == ThemeMode.Light);
        }
        else
        {
            // 不启用高亮时使用动态资源绑定前景色
            SetDynamicResource(codeTextBlock, TextBlock.ForegroundProperty,
                "Markdown.CodeBlock.Foreground",
                new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF)));
            codeTextBlock.Text = code;
        }

        codeScrollViewer.Content = codeTextBlock;
        return codeScrollViewer;
    }

    /// <summary>
    /// 设置动态资源引用
    /// </summary>
    private void SetDynamicResource(FrameworkElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        // 使用 TryFindResource 查找资源（会在整个资源树包括 MergedDictionaries 中查找）
        var resource = Application.Current?.TryFindResource(resourceKey);

        // 只有在整个资源树中都找不到时才添加默认值
        if (resource == null && Application.Current != null)
        {
            Application.Current.Resources[resourceKey] = defaultValue;
        }

        // 建立动态绑定
        element.SetResourceReference(property, resourceKey);
    }
}
