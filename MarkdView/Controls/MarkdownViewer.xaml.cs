using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text.RegularExpressions;
using Emoji.Wpf;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace MarkdView.Controls;

/// <summary>
/// MarkdView - 现代化 WPF Markdown 渲染控件
/// 特性:流式渲染、语法高亮、主题支持
/// 基于 Markdig AST 手动实现 WPF 渲染
/// </summary>
public partial class MarkdownViewer : UserControl
{
    #region 依赖属性

    /// <summary>
    /// Markdown 文本内容
    /// </summary>
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownViewer),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    /// <summary>
    /// 是否启用流式渲染(默认 true)
    /// </summary>
    public static readonly DependencyProperty EnableStreamingProperty =
        DependencyProperty.Register(
            nameof(EnableStreaming),
            typeof(bool),
            typeof(MarkdownViewer),
            new PropertyMetadata(true));

    /// <summary>
    /// 流式渲染防抖间隔(毫秒,默认 50)
    /// </summary>
    public static readonly DependencyProperty StreamingThrottleProperty =
        DependencyProperty.Register(
            nameof(StreamingThrottle),
            typeof(int),
            typeof(MarkdownViewer),
            new PropertyMetadata(50, OnStreamingThrottleChanged));

    /// <summary>
    /// 是否启用代码语法高亮(默认 true)
    /// </summary>
    public static readonly DependencyProperty EnableSyntaxHighlightingProperty =
        DependencyProperty.Register(
            nameof(EnableSyntaxHighlighting),
            typeof(bool),
            typeof(MarkdownViewer),
            new PropertyMetadata(true));

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

    #endregion

    #region 私有字段

    private readonly MarkdownPipeline _pipeline;
    private readonly DispatcherTimer _updateTimer;
    private string _lastRenderedText = string.Empty;
    private string _pendingText = string.Empty;
    private bool _hasPendingUpdate;

    #endregion

    #region 构造函数

    public MarkdownViewer()
    {
        InitializeComponent();

        // 配置 Markdig 管道 - 启用常用扩展
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseTaskLists()
            .Build();

        // 配置流式渲染定时器
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(StreamingThrottle)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        // 处理滚轮事件冒泡 - 使滚动在 ListView 等容器中正常工作
        MarkdownDocument.PreviewMouseWheel += OnPreviewMouseWheel;

        // 初始渲染
        RenderMarkdown();
    }

    #endregion

    #region 依赖属性回调

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer.OnMarkdownContentChanged((string)e.NewValue);
        }
    }

    private static void OnStreamingThrottleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            viewer._updateTimer.Interval = TimeSpan.FromMilliseconds((int)e.NewValue);
        }
    }

    #endregion

    #region Markdown 内容处理

    private void OnMarkdownContentChanged(string newText)
    {
        if (newText == null)
            newText = string.Empty;

        if (EnableStreaming)
        {
            // 流式渲染模式 - 使用防抖
            _pendingText = newText;
            _hasPendingUpdate = true;

            if (!_updateTimer.IsEnabled)
            {
                _updateTimer.Start();
            }
        }
        else
        {
            // 立即渲染模式
            _lastRenderedText = newText;
            RenderMarkdown();
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        if (_hasPendingUpdate)
        {
            _lastRenderedText = _pendingText;
            _hasPendingUpdate = false;
            RenderMarkdown();
        }
        else
        {
            _updateTimer.Stop();
        }
    }

    #endregion

    #region 核心渲染逻辑

    /// <summary>
    /// 渲染 Markdown 文本为 FlowDocument
    /// </summary>
    private void RenderMarkdown()
    {
        try
        {
            var flowDocument = ConvertMarkdownToFlowDocument(_lastRenderedText);
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
    /// 将 Markdown 文本转换为 FlowDocument
    /// </summary>
    private FlowDocument ConvertMarkdownToFlowDocument(string markdown)
    {
        // 解析 Markdown 为 AST
        var document = Markdig.Markdown.Parse(markdown ?? string.Empty, _pipeline);

        // 创建 FlowDocument 并应用基础样式
        var flowDocument = new FlowDocument
        {
            FontFamily = GetFontFamily("Markdown.FontFamily", "PingFang SC, Microsoft YaHei, sans-serif"),
            FontSize = GetFontSize("Markdown.FontSize", 15),
            LineHeight = GetDouble("Markdown.LineHeight", 1.6) * GetFontSize("Markdown.FontSize", 15),
            Foreground = GetBrush("Markdown.Foreground", Color.FromRgb(0x66, 0x66, 0x66)),
            Background = GetBrush("Markdown.Background", Colors.Transparent),
            PagePadding = new Thickness(0)
        };

        // 遍历所有块级元素
        foreach (var block in document)
        {
            var element = ConvertBlock(block);
            if (element != null)
            {
                flowDocument.Blocks.Add(element);
            }
        }

        // 使用 Emoji.Wpf 替换 emoji 字符为彩色 emoji
        flowDocument.SubstituteGlyphs();

        return flowDocument;
    }

    #endregion

    #region Block 转换

    /// <summary>
    /// 将 Markdig Block 转换为 WPF Block
    /// </summary>
    private WpfBlock? ConvertBlock(MarkdigBlock block)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading),
            ParagraphBlock paragraph => ConvertParagraph(paragraph),
            QuoteBlock quote => ConvertQuote(quote),
            ListBlock list => ConvertList(list),
            CodeBlock code => ConvertCodeBlock(code),
            ThematicBreakBlock => ConvertThematicBreak(),
            _ => null
        };
    }

    /// <summary>
    /// 转换标题块
    /// </summary>
    private WpfBlock ConvertHeading(HeadingBlock heading)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 10, 0, 10)
        };

        // 根据标题级别应用样式
        var level = heading.Level;
        var levelKey = $"H{level}";

        paragraph.FontSize = GetFontSize($"Markdown.Heading.{levelKey}.FontSize",
            level switch
            {
                1 => 28,
                2 => 24,
                3 => 20,
                4 => 17,
                5 => 15,
                6 => 15,
                _ => 16
            });

        paragraph.Foreground = GetBrush($"Markdown.Heading.{levelKey}.Foreground",
            Color.FromRgb(0x1A, 0x1A, 0x1A));

        paragraph.FontWeight = level <= 2 ? FontWeights.Bold : FontWeights.SemiBold;

        // H1 和 H2 添加底部边框
        if (level <= 2)
        {
            paragraph.BorderBrush = GetBrush($"Markdown.Heading.{levelKey}.Border",
                Color.FromRgb(0xE0, 0xE0, 0xE0));
            paragraph.BorderThickness = GetThickness("Markdown.Heading.BorderThickness", new Thickness(0, 0, 0, 1));
            paragraph.Padding = new Thickness(0, 0, 0, 8);
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
            Margin = new Thickness(0, 8, 0, 8),
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
    private WpfBlock ConvertQuote(QuoteBlock quote)
    {
        var section = new Section
        {
            Background = GetBrush("Markdown.Quote.Background", Color.FromRgb(0xF0, 0xF0, 0xF0)),
            BorderBrush = GetBrush("Markdown.Quote.Border", Color.FromRgb(0x5C, 0x9D, 0xFF)),
            BorderThickness = GetThickness("Markdown.Quote.BorderThickness", new Thickness(4, 0, 0, 0)),
            Padding = GetThickness("Markdown.Quote.Padding", new Thickness(16, 12, 16, 12)),
            Margin = new Thickness(0, 16, 0, 16)
        };

        // 递归转换子块
        foreach (var block in quote)
        {
            var element = ConvertBlock(block);
            if (element != null)
                section.Blocks.Add(element);
        }

        return section;
    }

    /// <summary>
    /// 转换列表块
    /// </summary>
    private WpfBlock ConvertList(ListBlock list)
    {
        var wpfList = list.IsOrdered ? (WpfBlock)new List() : new List();

        if (wpfList is List listElement)
        {
            listElement.Margin = new Thickness(0, 8, 0, 8);
            listElement.Padding = new Thickness(0, 0, 0, 0);
            listElement.MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;

            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    var listItemElement = new ListItem();

                    foreach (var block in listItem)
                    {
                        var element = ConvertBlock(block);
                        if (element != null)
                            listItemElement.Blocks.Add(element);
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
    private WpfBlock ConvertCodeBlock(CodeBlock codeBlock)
    {
        // 获取代码内容和语言
        var code = codeBlock is FencedCodeBlock fenced ? fenced.Lines.ToString() :
                   codeBlock is CodeBlock cb ? cb.Lines.ToString() : string.Empty;
        var language = codeBlock is FencedCodeBlock fencedCode ? fencedCode.Info : string.Empty;

        // 创建代码块容器
        var codeContainer = new Grid
        {
            Margin = new Thickness(0, 16, 0, 16)
        };

        // 主边框
        var mainBorder = new Border
        {
            Background = GetBrush("Markdown.CodeBlock.Background", Color.FromRgb(0x28, 0x2C, 0x34)),
            BorderBrush = GetBrush("Markdown.CodeBlock.Border", Color.FromRgb(0x21, 0x25, 0x2B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };

        var containerGrid = new Grid();
        containerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // 标题栏
        containerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // 代码内容

        // ===== 标题栏 =====
        var headerGrid = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x2B))
        };
        Grid.SetRow(headerGrid, 0);

        // 左侧：Mac 风格三个圆点
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

        headerGrid.Children.Add(dotsPanel);

        // 中间：语言标签（如果有）
        if (!string.IsNullOrEmpty(language))
        {
            var langLabel = new System.Windows.Controls.TextBlock
            {
                Text = language,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.Children.Add(langLabel);
        }

        // 右侧：复制按钮
        var copyButton = new Button
        {
            Content = "复制",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x48)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF)),
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Cursor = Cursors.Hand,
            Tag = code, // 将代码内容存储在 Tag 中
            Template = CreateCopyButtonTemplate() // 直接设置模板
        };

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

        headerGrid.Children.Add(copyButton);
        containerGrid.Children.Add(headerGrid);

        // ===== 代码内容区域 =====
        var codeScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16, 12, 16, 12)
        };
        Grid.SetRow(codeScrollViewer, 1);

        var codeTextBlock = new System.Windows.Controls.TextBlock
        {
            FontFamily = GetFontFamily("Markdown.CodeFontFamily", "Consolas, Monaco, Courier New, monospace"),
            FontSize = GetFontSize("Markdown.CodeFontSize", 13.5),
            Foreground = GetBrush("Markdown.CodeBlock.Foreground", Color.FromRgb(0xAB, 0xB2, 0xBF)),
            Text = code,
            TextWrapping = TextWrapping.NoWrap
        };

        // TODO: 应用语法高亮（如果需要的话，可以用 Inlines 代替 Text）
        if (EnableSyntaxHighlighting && !string.IsNullOrEmpty(language))
        {
            // 这里可以应用语法高亮，但由于我们用的是 TextBlock 而不是 FlowDocument
            // 需要使用 Inlines 来实现高亮
            codeTextBlock.Text = string.Empty;
            ApplySyntaxHighlightingToTextBlock(codeTextBlock, code, language);
        }

        codeScrollViewer.Content = codeTextBlock;
        containerGrid.Children.Add(codeScrollViewer);

        mainBorder.Child = containerGrid;
        codeContainer.Children.Add(mainBorder);

        // 使用 BlockUIContainer 包装
        return new BlockUIContainer(codeContainer);
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
    /// 对 TextBlock 应用语法高亮
    /// </summary>
    private void ApplySyntaxHighlightingToTextBlock(System.Windows.Controls.TextBlock textBlock, string code, string language)
    {
        // 简化的语法高亮实现
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            ApplySyntaxHighlightingToLineForTextBlock(textBlock, line);
            textBlock.Inlines.Add(new LineBreak());
        }

        // 移除最后一个多余的换行
        if (textBlock.Inlines.Count > 0 && textBlock.Inlines.LastInline is LineBreak)
        {
            textBlock.Inlines.Remove(textBlock.Inlines.LastInline);
        }
    }

    /// <summary>
    /// 对 TextBlock 的单行应用语法高亮
    /// </summary>
    private void ApplySyntaxHighlightingToLineForTextBlock(System.Windows.Controls.TextBlock textBlock, string line)
    {
        // 关键字正则
        var keywordPattern = @"\b(public|private|protected|class|interface|void|int|string|bool|var|return|if|else|for|while|new|using|namespace|static|async|await)\b";
        // 字符串正则
        var stringPattern = @"""([^""\\]|\\.)*""|'([^'\\]|\\.)*'";
        // 注释正则
        var commentPattern = @"//.*$|/\*[\s\S]*?\*/";
        // 数字正则
        var numberPattern = @"\b\d+(\.\d+)?\b";

        // 合并所有模式
        var combinedPattern = $"({commentPattern})|({stringPattern})|({keywordPattern})|({numberPattern})";
        var regex = new Regex(combinedPattern);

        int lastIndex = 0;
        foreach (Match match in regex.Matches(line))
        {
            // 添加匹配前的普通文本
            if (match.Index > lastIndex)
            {
                textBlock.Inlines.Add(new Run(line.Substring(lastIndex, match.Index - lastIndex)));
            }

            // 根据匹配类型添加高亮文本
            var run = new Run(match.Value);
            if (!string.IsNullOrEmpty(match.Groups[1].Value)) // 注释
                run.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x6A, 0x70));
            else if (!string.IsNullOrEmpty(match.Groups[2].Value)) // 字符串
                run.Foreground = new SolidColorBrush(Color.FromRgb(0x98, 0xC3, 0x79));
            else if (!string.IsNullOrEmpty(match.Groups[3].Value)) // 关键字
                run.Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x78, 0xDD));
            else if (!string.IsNullOrEmpty(match.Groups[4].Value)) // 数字
                run.Foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0x9A, 0x66));

            textBlock.Inlines.Add(run);
            lastIndex = match.Index + match.Length;
        }

        // 添加剩余的普通文本
        if (lastIndex < line.Length)
        {
            textBlock.Inlines.Add(new Run(line.Substring(lastIndex)));
        }
    }

    /// <summary>
    /// 转换水平分隔线
    /// </summary>
    private WpfBlock ConvertThematicBreak()
    {
        var paragraph = new Paragraph
        {
            Margin = GetThickness("Markdown.HorizontalRule.Margin", new Thickness(0, 8, 0, 8)),
            BorderBrush = GetBrush("Markdown.HorizontalRule.Border", Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderThickness = GetThickness("Markdown.HorizontalRule.BorderThickness", new Thickness(0, 1, 0, 0)),
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
            EmphasisInline emphasis => ConvertEmphasis(emphasis),
            CodeInline code => ConvertInlineCode(code),
            LineBreakInline => new LineBreak(),
            LinkInline link => ConvertLink(link),
            _ => null
        };
    }

    /// <summary>
    /// 转换文本内容
    /// Emoji 由 FlowDocument.SubstituteGlyphs() 统一处理
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

        // 粗体 (**)
        if (emphasis.DelimiterCount == 2)
        {
            span.FontWeight = FontWeights.Bold;
            span.Foreground = GetBrush("Markdown.Bold.Foreground", Color.FromRgb(0x5C, 0x9D, 0xFF));
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

    /// <summary>
    /// 转换行内代码
    /// </summary>
    private WpfInline ConvertInlineCode(CodeInline code)
    {
        var span = new Span
        {
            FontFamily = GetFontFamily("Markdown.CodeFontFamily", "Consolas, Monaco, Courier New, monospace"),
            FontSize = GetFontSize("Markdown.InlineCodeFontSize", 13),
            Background = GetBrush("Markdown.InlineCode.Background", Color.FromRgb(0xF0, 0xF0, 0xF0)),
            Foreground = GetBrush("Markdown.InlineCode.Foreground", Color.FromRgb(0xE5, 0x39, 0x35))
        };

        // 使用空格模拟 padding
        span.Inlines.Add(new Run(" " + code.Content + " "));
        return span;
    }

    /// <summary>
    /// 转换链接
    /// </summary>
    private WpfInline ConvertLink(LinkInline link)
    {
        var hyperlink = new Hyperlink
        {
            NavigateUri = link.Url != null ? new Uri(link.Url, UriKind.RelativeOrAbsolute) : null,
            Foreground = GetBrush("Markdown.Link.Foreground", Color.FromRgb(0x58, 0xA6, 0xFF)),
            TextDecorations = TextDecorations.Underline
        };

        hyperlink.RequestNavigate += (s, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.ToString(),
                    UseShellExecute = true
                });
            }
            catch { }
        };

        // 转换链接文本
        if (link.FirstChild != null)
        {
            foreach (var child in link)
            {
                var childElement = ConvertInline(child);
                if (childElement != null)
                    hyperlink.Inlines.Add(childElement);
            }
        }

        return hyperlink;
    }

    #endregion

    #region 语法高亮

    /// <summary>
    /// 应用代码语法高亮
    /// </summary>
    private void ApplySyntaxHighlighting(Paragraph paragraph, string code, string language)
    {
        // 简化的语法高亮实现
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            ApplySyntaxHighlightingToLine(paragraph, line);
            paragraph.Inlines.Add(new LineBreak());
        }

        // 移除最后一个多余的换行
        if (paragraph.Inlines.Count > 0 && paragraph.Inlines.LastInline is LineBreak)
        {
            paragraph.Inlines.Remove(paragraph.Inlines.LastInline);
        }
    }

    /// <summary>
    /// 对单行应用语法高亮
    /// </summary>
    private void ApplySyntaxHighlightingToLine(Paragraph paragraph, string line)
    {
        // 关键字正则
        var keywordPattern = @"\b(public|private|protected|class|interface|void|int|string|bool|var|return|if|else|for|while|new|using|namespace|static|async|await)\b";
        // 字符串正则
        var stringPattern = @"""([^""\\]|\\.)*""|'([^'\\]|\\.)*'";
        // 注释正则
        var commentPattern = @"//.*$|/\*[\s\S]*?\*/";
        // 数字正则
        var numberPattern = @"\b\d+\.?\d*\b";

        var lastIndex = 0;
        var matches = new List<(int Start, int Length, string Type)>();

        // 查找所有匹配
        foreach (Match match in Regex.Matches(line, commentPattern))
            matches.Add((match.Index, match.Length, "Comment"));
        foreach (Match match in Regex.Matches(line, stringPattern))
            matches.Add((match.Index, match.Length, "String"));
        foreach (Match match in Regex.Matches(line, keywordPattern))
            matches.Add((match.Index, match.Length, "Keyword"));
        foreach (Match match in Regex.Matches(line, numberPattern))
            matches.Add((match.Index, match.Length, "Number"));

        // 按位置排序
        matches = matches.OrderBy(m => m.Start).ToList();

        foreach (var match in matches)
        {
            // 添加前面的普通文本
            if (match.Start > lastIndex)
            {
                paragraph.Inlines.Add(new Run(line.Substring(lastIndex, match.Start - lastIndex)));
            }

            // 添加高亮文本
            var run = new Run(line.Substring(match.Start, match.Length));
            run.Foreground = match.Type switch
            {
                "Keyword" => GetBrush("Markdown.Syntax.Keyword", Color.FromRgb(0xC6, 0x78, 0xDD)),
                "String" => GetBrush("Markdown.Syntax.String", Color.FromRgb(0x98, 0xC3, 0x79)),
                "Comment" => GetBrush("Markdown.Syntax.Comment", Color.FromRgb(0x5C, 0x63, 0x70)),
                "Number" => GetBrush("Markdown.Syntax.Number", Color.FromRgb(0xD1, 0x9A, 0x66)),
                _ => GetBrush("Markdown.CodeBlock.Foreground", Color.FromRgb(0xAB, 0xB2, 0xBF))
            };
            paragraph.Inlines.Add(run);

            lastIndex = match.Start + match.Length;
        }

        // 添加剩余文本
        if (lastIndex < line.Length)
        {
            paragraph.Inlines.Add(new Run(line.Substring(lastIndex)));
        }
    }

    #endregion

    #region 主题资源辅助方法

    /// <summary>
    /// 从主题资源获取 Brush
    /// </summary>
    private Brush GetBrush(string key, Color defaultColor)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (Brush)Application.Current.Resources[key];
        }
        return new SolidColorBrush(defaultColor);
    }

    /// <summary>
    /// 从主题资源获取 FontFamily
    /// </summary>
    private FontFamily GetFontFamily(string key, string defaultFont)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (FontFamily)Application.Current.Resources[key];
        }
        return new FontFamily(defaultFont);
    }

    /// <summary>
    /// 从主题资源获取字体大小
    /// </summary>
    private double GetFontSize(string key, double defaultSize)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (double)Application.Current.Resources[key];
        }
        return defaultSize;
    }

    /// <summary>
    /// 从主题资源获取 Double 值
    /// </summary>
    private double GetDouble(string key, double defaultValue)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (double)Application.Current.Resources[key];
        }
        return defaultValue;
    }

    /// <summary>
    /// 从主题资源获取 Thickness
    /// </summary>
    private Thickness GetThickness(string key, Thickness defaultThickness)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (Thickness)Application.Current.Resources[key];
        }
        return defaultThickness;
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 处理鼠标滚轮事件 - 将事件冒泡到父级容器,使控件在 ListView 等容器中能够正常滚动
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 如果内部的 FlowDocumentScrollViewer 禁用了滚动条,则将事件传递给父级
        if (MarkdownDocument.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled)
        {
            // 不处理事件,让它继续冒泡
            // 但是我们需要手动触发父级的滚动
            if (e.Handled)
            {
                return;
            }

            var scrollViewer = FindParentScrollViewer(this);
            if (scrollViewer != null)
            {
                // 直接操作父级 ScrollViewer 的滚动
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
