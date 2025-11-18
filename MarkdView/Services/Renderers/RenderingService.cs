using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdView.Services.Theme;
using MarkdView.Services.SyntaxHighlight;
using MarkdView.Renderers;
using Emoji.Wpf;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using MarkdigBlock = Markdig.Syntax.Block;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace MarkdView.Services.Renderers;

/// <summary>
/// Markdown 渲染服务 - 负责将 Markdown 文本转换为 WPF FlowDocument
/// </summary>
public class RenderingService
{
    private readonly MarkdownPipeline _pipeline;

    public RenderingService(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    /// <summary>
    /// 将 Markdown 文本转换为 FlowDocument
    /// </summary>
    public FlowDocument ConvertMarkdownToFlowDocument(
        string markdown,
        FontFamily fontFamily,
        double fontSize,
        bool enableSyntaxHighlighting,
        CodeBlockRenderer? codeBlockRenderer = null)
    {
        // 解析 Markdown 为 AST
        var document = Markdown.Parse(markdown ?? string.Empty, _pipeline);

        // 创建 FlowDocument 并应用基础样式
        var flowDocument = new FlowDocument
        {
            FontFamily = fontFamily,
            FontSize = fontSize,
            LineHeight = GetDouble("Markdown.LineHeight", 1.6) * fontSize,
            PagePadding = new Thickness(0)
        };

        // 使用动态资源绑定 FlowDocument 的前景色和背景色
        SetDynamicResource(flowDocument, FlowDocument.ForegroundProperty, "Markdown.Foreground",
            new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)));
        SetDynamicResource(flowDocument, FlowDocument.BackgroundProperty, "Markdown.Background",
            new SolidColorBrush(Colors.Transparent));

        // 遍历所有块级元素
        foreach (var block in document)
        {
            var element = ConvertBlock(block, enableSyntaxHighlighting, codeBlockRenderer);
            if (element != null)
            {
                flowDocument.Blocks.Add(element);
            }
        }

        // 使用 Emoji.Wpf 替换 emoji 字符为彩色 emoji
        flowDocument.SubstituteGlyphs();

        return flowDocument;
    }

    #region Block 转换

    /// <summary>
    /// 将 Markdig Block 转换为 WPF Block
    /// </summary>
    private WpfBlock? ConvertBlock(MarkdigBlock block, bool enableSyntaxHighlighting, CodeBlockRenderer? codeBlockRenderer)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading),
            ParagraphBlock paragraph => ConvertParagraph(paragraph),
            QuoteBlock quote => ConvertQuote(quote, enableSyntaxHighlighting, codeBlockRenderer),
            ListBlock list => ConvertList(list, enableSyntaxHighlighting, codeBlockRenderer),
            CodeBlock code => ConvertCodeBlock(code, codeBlockRenderer),
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

        // H3 使用 H2 的样式
        var styleKey = level == 3 ? "H2" : levelKey;

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

        // 使用动态资源绑定标题前景色（H3 使用 H2 的颜色）
        SetDynamicResource(paragraph, Paragraph.ForegroundProperty,
            $"Markdown.Heading.{styleKey}.Foreground",
            new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)));

        paragraph.FontWeight = level <= 3 ? FontWeights.Bold : FontWeights.SemiBold;

        // H1, H2 和 H3 添加底部边框
        if (level <= 3)
        {
            // 使用动态资源绑定边框颜色（H3 使用 H2 的边框颜色）
            SetDynamicResource(paragraph, Paragraph.BorderBrushProperty,
                $"Markdown.Heading.{styleKey}.Border",
                new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)));
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
    private WpfBlock ConvertQuote(QuoteBlock quote, bool enableSyntaxHighlighting, CodeBlockRenderer? codeBlockRenderer)
    {
        var section = new Section
        {
            BorderThickness = GetThickness("Markdown.Quote.BorderThickness", new Thickness(4, 0, 0, 0)),
            Padding = GetThickness("Markdown.Quote.Padding", new Thickness(16, 12, 16, 12)),
            Margin = new Thickness(0, 16, 0, 16)
        };

        // 使用动态资源绑定引用块的背景和边框颜色
        SetDynamicResource(section, Section.BackgroundProperty,
            "Markdown.Quote.Background",
            new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)));
        SetDynamicResource(section, Section.BorderBrushProperty,
            "Markdown.Quote.Border",
            new SolidColorBrush(Color.FromRgb(0x5C, 0x9D, 0xFF)));

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
    private WpfBlock ConvertList(ListBlock list, bool enableSyntaxHighlighting, CodeBlockRenderer? codeBlockRenderer)
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
                        var element = ConvertBlock(block, enableSyntaxHighlighting, codeBlockRenderer);
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
    private WpfBlock ConvertCodeBlock(CodeBlock codeBlock, CodeBlockRenderer? codeBlockRenderer)
    {
        // 获取代码内容和语言
        var code = codeBlock is FencedCodeBlock fenced ? fenced.Lines.ToString() :
                   codeBlock is CodeBlock cb ? cb.Lines.ToString() : string.Empty;
        var language = codeBlock is FencedCodeBlock fencedCode ? fencedCode.Info : string.Empty;

        // 使用代码块渲染器（如果提供）
        if (codeBlockRenderer != null)
        {
            return codeBlockRenderer.Render(code, language);
        }

        // 默认简单渲染
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 8, 0, 8),
            Background = GetBrush("Markdown.CodeBlock.Background", Color.FromRgb(0x28, 0x2C, 0x34)),
            Foreground = GetBrush("Markdown.CodeBlock.Foreground", Color.FromRgb(0xAB, 0xB2, 0xBF)),
            FontFamily = GetFontFamily("Markdown.CodeFontFamily", "Consolas, Monaco, Courier New, monospace"),
            FontSize = GetFontSize("Markdown.CodeFontSize", 13),
            Padding = new Thickness(12)
        };

        paragraph.Inlines.Add(new Run(code));
        return paragraph;
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
            FontSize = GetFontSize("Markdown.InlineCodeFontSize", 13)
        };

        // 使用动态资源绑定内联代码的背景和前景色
        SetDynamicResource(span, Span.BackgroundProperty,
            "Markdown.InlineCode.Background",
            new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)));
        SetDynamicResource(span, Span.ForegroundProperty,
            "Markdown.InlineCode.Foreground",
            new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)));

        // 使用空格模拟 padding
        span.Inlines.Add(new Run(" " + code.Content + " "));
        return span;
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

        var hyperlink = new Hyperlink
        {
            NavigateUri = link.Url != null ? new Uri(link.Url, UriKind.RelativeOrAbsolute) : null,
            TextDecorations = TextDecorations.Underline
        };

        // 使用动态资源绑定链接颜色
        SetDynamicResource(hyperlink, Hyperlink.ForegroundProperty,
            "Markdown.Link.Foreground",
            new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)));

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

    /// <summary>
    /// 转换图片
    /// </summary>
    private WpfInline ConvertImage(LinkInline image)
    {
        if (string.IsNullOrEmpty(image.Url))
            return new Run("[图片加载失败]");

        try
        {
            var imageControl = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(image.Url, UriKind.RelativeOrAbsolute)),
                Stretch = Stretch.Uniform,
                MaxWidth = 800,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 8),
                Tag = image.Url
            };

            // 添加图片加载失败的处理
            imageControl.ImageFailed += (s, e) =>
            {
                if (s is System.Windows.Controls.Image img)
                {
                    var textBlock = new System.Windows.Controls.TextBlock
                    {
                        Text = $"[图片加载失败: {img.Tag}]",
                        Foreground = Brushes.Red,
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(0, 8, 0, 8)
                    };

                    // 替换失败的图片为文本
                    var parent = VisualTreeHelper.GetParent(img) as FrameworkElement;
                    if (parent != null)
                    {
                        var container = parent.Parent as BlockUIContainer;
                        if (container != null)
                        {
                            container.Child = textBlock;
                        }
                    }
                }
            };

            // 为图片添加边框
            var border = new Border
            {
                Child = imageControl,
                Background = GetBrush("Markdown.Image.Background", Colors.Transparent),
                BorderBrush = GetBrush("Markdown.Image.Border", Color.FromRgb(0xCC, 0xCC, 0xCC)),
                BorderThickness = GetThickness("Markdown.Image.BorderThickness", new Thickness(0)),
                CornerRadius = GetCornerRadius("Markdown.Image.CornerRadius", new CornerRadius(4)),
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // 如果有替代文本，作为工具提示
            if (image.FirstChild != null)
            {
                var tooltip = new ToolTip();
                var tooltipTextBlock = new System.Windows.Controls.TextBlock
                {
                    Text = image.FirstChild.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300
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
                Foreground = Brushes.Red,
                FontStyle = FontStyles.Italic
            };
        }
    }

    #endregion

    #region 主题资源辅助方法

    /// <summary>
    /// 设置动态资源引用（用于 FrameworkContentElement）
    /// </summary>
    private void SetDynamicResource(FrameworkContentElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        // 先确保资源字典中有这个键（如果没有就添加默认值）
        if (Application.Current?.Resources.Contains(resourceKey) != true)
        {
            Application.Current!.Resources[resourceKey] = defaultValue;
        }

        // 总是建立动态绑定
        element.SetResourceReference(property, resourceKey);
    }

    /// <summary>
    /// 设置动态资源引用（用于 TextElement）
    /// </summary>
    private void SetDynamicResource(System.Windows.Documents.TextElement element, DependencyProperty property, string resourceKey, object defaultValue)
    {
        // 先确保资源字典中有这个键（如果没有就添加默认值）
        if (Application.Current?.Resources.Contains(resourceKey) != true)
        {
            Application.Current!.Resources[resourceKey] = defaultValue;
        }

        // 总是建立动态绑定
        element.SetResourceReference(property, resourceKey);
    }

    private Brush GetBrush(string key, Color defaultColor)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (Brush)Application.Current.Resources[key];
        }
        return new SolidColorBrush(defaultColor);
    }

    private FontFamily GetFontFamily(string key, string defaultFont)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (FontFamily)Application.Current.Resources[key];
        }
        return new FontFamily(defaultFont);
    }

    private double GetFontSize(string key, double defaultSize)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (double)Application.Current.Resources[key];
        }
        return defaultSize;
    }

    private double GetDouble(string key, double defaultValue)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (double)Application.Current.Resources[key];
        }
        return defaultValue;
    }

    private Thickness GetThickness(string key, Thickness defaultThickness)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (Thickness)Application.Current.Resources[key];
        }
        return defaultThickness;
    }

    private CornerRadius GetCornerRadius(string key, CornerRadius defaultCornerRadius)
    {
        if (Application.Current?.Resources.Contains(key) == true)
        {
            return (CornerRadius)Application.Current.Resources[key];
        }
        return defaultCornerRadius;
    }

    #endregion
}
