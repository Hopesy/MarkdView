using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Markdig;
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

namespace MarkdView.Renderers;

/// <summary>
/// Markdown 渲染器 - 负责将 Markdown 文本转换为 WPF FlowDocument
/// </summary>
public class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private double _baseFontSize;
    private FontFamily _fontFamily;
    private static readonly HashSet<string> SafeExternalSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        Uri.UriSchemeMailto
    };

    public MarkdownRenderer(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
        _baseFontSize = 12.0;
        _fontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
    }

    /// <summary>
    /// 将 Markdown 文本转换为 FlowDocument
    /// </summary>
    public FlowDocument ConvertMarkdownToFlowDocument(
        string markdown,
        FontFamily fontFamily,
        double fontSize,
        bool enableSyntaxHighlighting,
        CodeBlockRenderer? codeBlockRenderer = null,
        bool useTransparentCanvas = false)
    {
        // 保存基础字体设置，供子元素使用
        _baseFontSize = fontSize;
        _fontFamily = fontFamily;

        // 解析 Markdown 为 AST
        var document = Markdown.Parse(markdown ?? string.Empty, _pipeline);

        // 创建 FlowDocument 并应用基础样式
        var flowDocument = new FlowDocument
        {
            FontFamily = fontFamily,
            FontSize = fontSize,
            LineHeight = GetDouble("Markdown.LineHeight", 1.68) * fontSize,
            PagePadding = GetThickness("Markdown.PagePadding", new Thickness(2, 4, 2, 4))
        };

        // 使用动态资源绑定 FlowDocument 的前景色和背景色（默认深色主题）
        SetDynamicResource(flowDocument, FlowDocument.ForegroundProperty, "Markdown.Foreground",
            new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));
        if (useTransparentCanvas)
        {
            flowDocument.Background = Brushes.Transparent;
        }
        else
        {
            SetDynamicResource(flowDocument, FlowDocument.BackgroundProperty, "Markdown.Background",
                new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)));
        }

        var effectiveCodeBlockRenderer = codeBlockRenderer
            ?? new CodeBlockRenderer(enableSyntaxHighlighting, baseFontSize: fontSize);

        // 遍历所有块级元素
        foreach (var block in document)
        {
            var element = ConvertBlock(block, enableSyntaxHighlighting, effectiveCodeBlockRenderer);
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
    private WpfBlock? ConvertBlock(MarkdigBlock block, bool enableSyntaxHighlighting, CodeBlockRenderer codeBlockRenderer, int listLevel = 0)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading),
            ParagraphBlock paragraph => ConvertParagraph(paragraph),
            QuoteBlock quote => ConvertQuote(quote, enableSyntaxHighlighting, codeBlockRenderer),
            ListBlock list => ConvertList(list, enableSyntaxHighlighting, codeBlockRenderer, listLevel),
            MarkdigTable table => ConvertTable(table, enableSyntaxHighlighting, codeBlockRenderer),
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
        var level = heading.Level;
        var paragraph = new Paragraph
        {
            Margin = level switch
            {
                1 => new Thickness(0, 24, 0, 14),
                2 => new Thickness(0, 20, 0, 12),
                3 => new Thickness(0, 16, 0, 10),
                _ => new Thickness(0, 12, 0, 8)
            }
        };

        var levelKey = $"H{level}";

        // H3 使用 H2 的样式
        var styleKey = level == 3 ? "H2" : levelKey;

        // 标题字体大小基于基础字体大小按比例缩放
        // 比例系数：H1=1.5, H2=1.25, H3=1.17, H4=1.08, H5=1.0, H6=1.0
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
            paragraph.BorderThickness = GetThickness("Markdown.Heading.BorderThickness", new Thickness(0, 0, 0, 1));
            paragraph.Padding = new Thickness(0, 0, 0, level == 1 ? 10 : 8);
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
            Margin = new Thickness(0, 6, 0, 10),
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
            BorderThickness = GetThickness("Markdown.Quote.BorderThickness", new Thickness(3, 0, 0, 0)),
            Padding = GetThickness("Markdown.Quote.Padding", new Thickness(14, 10, 14, 10)),
            Margin = new Thickness(0, 14, 0, 16)
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
            listElement.Margin = new Thickness(0, 6, 0, 10);
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
            listElement.StartIndex = 1;     // 有序列表起始序号

            // 不设置 Foreground，让标记继承 FlowDocument 的前景色

            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    var listItemElement = new ListItem
                    {
                        Margin = new Thickness(0, 2, 0, 6),  // 列表项之间的间距
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
                                para.LineHeight = _baseFontSize * GetDouble("Markdown.LineHeight", 1.68);
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
            Margin = new Thickness(0, 12, 0, 14),
            BorderThickness = new Thickness(1)
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
                    Padding = new Thickness(10, 6, 10, 6),
                    BorderThickness = new Thickness(0, 0, 1, 1),
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
                        paragraph.LineHeight = _baseFontSize * GetDouble("Markdown.LineHeight", 1.68);
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
            Margin = GetThickness("Markdown.HorizontalRule.Margin", new Thickness(0, 18, 0, 18)),
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

    /// <summary>
    /// 转换行内代码
    /// </summary>
    private WpfInline ConvertInlineCode(CodeInline code)
    {
        var inlineFontSize = _baseFontSize * 0.88;
        var codeText = new System.Windows.Controls.TextBlock
        {
            Text = code.Content,
            FontFamily = GetFontFamily("Markdown.CodeFontFamily", "Consolas, Monaco, Courier New, monospace"),
            FontSize = inlineFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            LineHeight = inlineFontSize * 1.18,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };

        SetDynamicResource(codeText, System.Windows.Controls.TextBlock.ForegroundProperty,
            "Markdown.InlineCode.Foreground",
            new SolidColorBrush(Color.FromRgb(0xF9, 0x82, 0x66)));

        var textHost = new Grid
        {
            MinHeight = Math.Ceiling(inlineFontSize * 1.5),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        textHost.Children.Add(codeText);

        var border = new Border
        {
            Child = textHost,
            Padding = new Thickness(6, 0, 6, 0),
            CornerRadius = GetCornerRadius("Markdown.InlineCode.CornerRadius", new CornerRadius(4)),
            BorderThickness = GetThickness("Markdown.InlineCode.BorderThickness", new Thickness(1)),
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

        var hyperlink = new Hyperlink
        {
            NavigateUri = safeUri,
            TextDecorations = null,
            FontWeight = FontWeights.SemiBold
        };

        // 使用动态资源绑定链接颜色（默认深色主题）
        SetDynamicResource(hyperlink, Hyperlink.ForegroundProperty,
            "Markdown.Link.Foreground",
            new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));

        hyperlink.RequestNavigate += (s, e) =>
        {
            if (e.Uri == null || !SafeExternalSchemes.Contains(e.Uri.Scheme))
            {
                return;
            }

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
        AppendInlineChildren(link, hyperlink.Inlines);

        return hyperlink;
    }

    internal static bool TryCreateSafeNavigateUri(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (!SafeExternalSchemes.Contains(candidate.Scheme))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

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
