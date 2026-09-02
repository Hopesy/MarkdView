using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Emoji.Wpf;
using MarkdView.Documents;
using MarkdView.Media;

namespace MarkdView.Renderers;

/// <summary>
/// 已迁移到稳定文档模型的 WPF block renderer。
/// 结构和文本由模型驱动，图片与链接的副作用分别通过独立适配器注入；未覆盖节点继续由兼容 renderer 处理。
/// </summary>
internal sealed class WpfSimpleMarkdownRenderer
{
    private readonly WpfMarkdownLinkNavigator? _linkNavigator;
    private readonly WpfMarkdownImageLoader? _imageLoader;
    private readonly Func<MarkdownRenderOptions, MarkdownImageLoadOptions>? _imageOptionsFactory;
    private readonly Func<MarkdownRenderOptions, int>? _maxImagesFactory;
    private readonly Func<MarkdownRenderOptions, CodeBlockRenderer>? _codeBlockRendererFactory;
    private MarkdownRenderSession? _activeRenderSession;
    private MarkdownImageLoadOptions _activeImageLoadOptions
        = MarkdownRenderDefaults.CreateImageLoadOptions();
    private int _activeMaxImagesPerDocument = MarkdownRenderDefaults.MaxImagesPerDocument;

    public WpfSimpleMarkdownRenderer(
        WpfMarkdownLinkNavigator? linkNavigator = null,
        WpfMarkdownImageLoader? imageLoader = null,
        Func<MarkdownRenderOptions, MarkdownImageLoadOptions>? imageOptionsFactory = null,
        Func<MarkdownRenderOptions, int>? maxImagesFactory = null,
        Func<MarkdownRenderOptions, CodeBlockRenderer>? codeBlockRendererFactory = null)
    {
        _linkNavigator = linkNavigator;
        _imageLoader = imageLoader;
        _imageOptionsFactory = imageOptionsFactory;
        _maxImagesFactory = maxImagesFactory;
        _codeBlockRendererFactory = codeBlockRendererFactory;
    }

    private static readonly HashSet<MarkdownInlineKind> SupportedInlineKinds = new()
    {
        MarkdownInlineKind.Text,
        MarkdownInlineKind.Emphasis,
        MarkdownInlineKind.Strong,
        MarkdownInlineKind.Strikethrough,
        MarkdownInlineKind.Code,
        MarkdownInlineKind.LineBreak,
        MarkdownInlineKind.Html,
        MarkdownInlineKind.Task,
        MarkdownInlineKind.Link,
        MarkdownInlineKind.Autolink,
        MarkdownInlineKind.Image
    };

    public bool TryRender(
        MarkdownDocumentModel model,
        MarkdownRenderOptions options,
        out FlowDocument? document)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);

        if (!model.Blocks.All(IsSupportedBlock))
        {
            document = null;
            return false;
        }

        var codeBlockRenderer = PrepareRender(options);
        if (model.Blocks.SelectMany(FlattenBlocks).Any(block => block.Kind == MarkdownBlockKind.Code)
            && codeBlockRenderer == null)
        {
            document = null;
            return false;
        }

        var flowDocument = CreateFlowDocument(options);

        foreach (var block in model.Blocks)
        {
            flowDocument.Blocks.Add(RenderSupportedBlock(
                block,
                options.FontFamily,
                options.FontSize,
                codeBlockRenderer));
        }

        flowDocument.SubstituteGlyphs();
        document = flowDocument;
        return true;
    }

    /// <summary>
    /// 判断一个顶层块是否可以由模型 renderer 直接处理。
    /// 混合适配器使用此方法决定是否只对当前块执行兼容回退。
    /// </summary>
    internal bool CanRenderBlock(MarkdownBlockModel block)
        => IsSupportedBlock(block);

    internal bool CanRenderBlock(
        MarkdownBlockModel block,
        CodeBlockRenderer? codeBlockRenderer)
        => IsSupportedBlock(block)
            && (codeBlockRenderer != null
                || !FlattenBlocks(block).Any(candidate => candidate.Kind == MarkdownBlockKind.Code));

    /// <summary>
    /// 初始化一次模型渲染会话。调用方可以在同一个 FlowDocument 中渲染多个块，
    /// 从而保证图片计数和取消令牌覆盖整个文档，而不是每个块各自重置。
    /// </summary>
    internal CodeBlockRenderer? PrepareRender(
        MarkdownRenderOptions options,
        MarkdownRenderSession? sharedSession = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_activeRenderSession != null && !ReferenceEquals(_activeRenderSession, sharedSession))
        {
            _activeRenderSession.Dispose();
        }

        _activeRenderSession = sharedSession ?? new MarkdownRenderSession();
        _activeImageLoadOptions = options.ImageLoadOptions
            ?? _imageOptionsFactory?.Invoke(options)
            ?? MarkdownRenderDefaults.CreateImageLoadOptions();
        _activeMaxImagesPerDocument = options.MaxImagesPerDocument
            ?? _maxImagesFactory?.Invoke(options)
            ?? 64;
        if (_activeMaxImagesPerDocument is < 0 or > MarkdownRenderDefaults.MaxImagesPerDocumentLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                _activeMaxImagesPerDocument,
                $"文档图片数量限制必须在 0 到 {MarkdownRenderDefaults.MaxImagesPerDocumentLimit} 之间。");
        }
        return options.CodeBlockRenderer ?? _codeBlockRendererFactory?.Invoke(options);
    }

    internal FlowDocument CreateFlowDocument(MarkdownRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var flowDocument = new FlowDocument
        {
            FontFamily = options.FontFamily,
            FontSize = options.FontSize,
            LineHeight = GetDouble("Markdown.LineHeight", MarkdownLayoutDefaults.LineHeightScale) * options.FontSize,
            PagePadding = GetThickness("Markdown.PagePadding", MarkdownLayoutDefaults.PagePadding)
        };

        if (options.Foreground != null)
        {
            flowDocument.Foreground = options.Foreground;
        }
        else
        {
            SetDynamicResource(
                flowDocument,
                FlowDocument.ForegroundProperty,
                "Markdown.Foreground",
                new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));
        }

        if (options.UseTransparentCanvas)
        {
            flowDocument.Background = Brushes.Transparent;
        }
        else
        {
            SetDynamicResource(
                flowDocument,
                FlowDocument.BackgroundProperty,
                "Markdown.Background",
                new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)));
        }

        return flowDocument;
    }

    internal Block RenderSupportedBlock(
        MarkdownBlockModel block,
        FontFamily fontFamily,
        double baseFontSize,
        CodeBlockRenderer? codeBlockRenderer)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (!CanRenderBlock(block, codeBlockRenderer))
        {
            throw new InvalidOperationException(
                $"模型块 {block.Kind} 包含模型 renderer 尚未覆盖的语义。");
        }

        return RenderBlock(block, fontFamily, baseFontSize, listLevel: 0, codeBlockRenderer);
    }

    public void CancelPendingOperations()
    {
        _activeRenderSession?.Cancel();
    }

    internal MarkdownRenderSession? ActiveRenderSession => _activeRenderSession;

    private static IEnumerable<MarkdownBlockModel> FlattenBlocks(MarkdownBlockModel block)
    {
        yield return block;
        foreach (var child in block.Children.SelectMany(FlattenBlocks))
        {
            yield return child;
        }
    }

    private static bool IsSupportedBlock(MarkdownBlockModel block)
    {
        if (block.RequiresCompatibilityRenderer)
        {
            return false;
        }

        return block.Kind switch
        {
            MarkdownBlockKind.Heading or MarkdownBlockKind.Paragraph
                => block.Inlines.All(IsSupportedInline),
            MarkdownBlockKind.Quote
                => block.Children.All(IsSupportedBlock),
            MarkdownBlockKind.List
                => block.Children.All(child =>
                    child.Kind == MarkdownBlockKind.ListItem && IsSupportedBlock(child)),
            MarkdownBlockKind.ListItem
                => block.Children.Count > 0 && block.Children.All(IsSupportedBlock),
            MarkdownBlockKind.Table
                => block.Children.All(child =>
                    child.Kind == MarkdownBlockKind.TableRow && IsSupportedBlock(child)),
            MarkdownBlockKind.TableRow
                => block.Children.All(child =>
                    child.Kind == MarkdownBlockKind.TableCell && IsSupportedBlock(child)),
            MarkdownBlockKind.TableCell
                => block.Children.Count > 0 && block.Children.All(IsSupportedBlock),
            MarkdownBlockKind.Code => block.CodeText != null && block.Children.Count == 0,
            MarkdownBlockKind.ThematicBreak => true,
            MarkdownBlockKind.Html => true,
            _ => false
        };
    }

    private static bool IsSupportedInline(MarkdownInlineModel inline)
        => !inline.RequiresCompatibilityRenderer
            && SupportedInlineKinds.Contains(inline.Kind)
            && inline.Children.All(IsSupportedInline);

    private Block RenderBlock(
        MarkdownBlockModel block,
        FontFamily fontFamily,
        double baseFontSize,
        int listLevel,
        CodeBlockRenderer? codeBlockRenderer)
    {
        if (block.Kind == MarkdownBlockKind.Heading)
        {
            var level = Math.Clamp(block.HeadingLevel, 1, 6);
            var paragraph = new Paragraph
            {
                Margin = GetThickness($"Markdown.Heading.H{level}.Margin", level switch
                {
                    1 => MarkdownLayoutDefaults.HeadingMargin(1),
                    2 => MarkdownLayoutDefaults.HeadingMargin(2),
                    3 => MarkdownLayoutDefaults.HeadingMargin(3),
                    _ => MarkdownLayoutDefaults.HeadingMargin(level)
                }),
                FontSize = baseFontSize * level switch
                {
                    1 => 1.75,
                    2 => 1.42,
                    3 => 1.24,
                    4 => 1.12,
                    5 => 1.02,
                    _ => 0.96
                },
                FontWeight = level <= 2 ? FontWeights.Bold : level == 3 ? FontWeights.SemiBold : FontWeights.Medium,
                LineHeight = baseFontSize * (level switch
                {
                    1 => 1.75,
                    2 => 1.42,
                    3 => 1.24,
                    4 => 1.12,
                    5 => 1.02,
                    _ => 0.96
                }) * 1.24,
                Padding = GetThickness(
                    $"Markdown.Heading.H{level}.Padding",
                    MarkdownLayoutDefaults.HeadingPadding(level))
            };

            SetDynamicResource(
                paragraph,
                Paragraph.ForegroundProperty,
                $"Markdown.Heading.H{level}.Foreground",
                new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)));

            if (level <= 3)
            {
                SetDynamicResource(
                    paragraph,
                    Paragraph.BorderBrushProperty,
                    $"Markdown.Heading.H{level}.Border",
                    new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
                paragraph.BorderThickness = GetThickness(
                    "Markdown.Heading.BorderThickness",
                    MarkdownLayoutDefaults.HeadingBorderThickness);
            }

            AppendInlines(block.Inlines, paragraph.Inlines, baseFontSize);
            return paragraph;
        }

        if (block.Kind == MarkdownBlockKind.Quote)
        {
            var section = new Section
            {
                BorderThickness = GetThickness(
                    "Markdown.Quote.BorderThickness",
                    MarkdownLayoutDefaults.QuoteBorderThickness),
                Padding = GetThickness(
                    "Markdown.Quote.Padding",
                    MarkdownLayoutDefaults.QuotePadding),
                Margin = GetThickness(
                    "Markdown.Quote.Margin",
                    MarkdownLayoutDefaults.QuoteMargin)
            };

            SetDynamicResource(
                section,
                Section.BackgroundProperty,
                "Markdown.Quote.Background",
                new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)));
            SetDynamicResource(
                section,
                Section.BorderBrushProperty,
                "Markdown.Quote.Border",
                new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
            SetDynamicResource(
                section,
                Section.ForegroundProperty,
                "Markdown.Quote.Foreground",
                new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)));

            foreach (var child in block.Children)
            {
                section.Blocks.Add(RenderBlock(child, fontFamily, baseFontSize, listLevel, codeBlockRenderer));
            }

            return section;
        }

        if (block.Kind == MarkdownBlockKind.List)
        {
            return RenderList(block, fontFamily, baseFontSize, listLevel, codeBlockRenderer);
        }

        if (block.Kind == MarkdownBlockKind.ListItem)
        {
            // ListItem 通常由 RenderList 创建；Section 使异常的独立 ListItem
            // 仍然可以以稳定块的形式输出，而不会把非法节点加入 FlowDocument。
            var section = new Section();
            foreach (var child in block.Children)
            {
                section.Blocks.Add(RenderBlock(child, fontFamily, baseFontSize, listLevel, codeBlockRenderer));
            }

            return section;
        }

        if (block.Kind == MarkdownBlockKind.ThematicBreak)
        {
            var separator = new Paragraph
            {
                Margin = GetThickness(
                    "Markdown.HorizontalRule.Margin",
                    MarkdownLayoutDefaults.HorizontalRuleMargin),
                BorderThickness = GetThickness(
                    "Markdown.HorizontalRule.BorderThickness",
                    MarkdownLayoutDefaults.HorizontalRuleBorderThickness),
                Padding = new Thickness(0)
            };
            SetDynamicResource(
                separator,
                Paragraph.BorderBrushProperty,
                "Markdown.HorizontalRule.Border",
                new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)));
            return separator;
        }

        if (block.Kind == MarkdownBlockKind.Table)
        {
            return RenderTable(block, fontFamily, baseFontSize, codeBlockRenderer);
        }

        if (block.Kind == MarkdownBlockKind.Code)
        {
            return codeBlockRenderer!.Render(block.CodeText ?? string.Empty, block.Language);
        }

        if (block.Kind == MarkdownBlockKind.Html)
        {
            return new Paragraph(new Run(block.SourceText))
            {
                Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin),
                FontFamily = fontFamily
            };
        }

        var paragraphBlock = new Paragraph
        {
            Margin = GetThickness("Markdown.Paragraph.Margin", MarkdownLayoutDefaults.ParagraphMargin),
            FontFamily = fontFamily
        };
        AppendInlines(block.Inlines, paragraphBlock.Inlines, baseFontSize);
        return paragraphBlock;
    }

    private System.Windows.Documents.List RenderList(
        MarkdownBlockModel block,
        FontFamily fontFamily,
        double baseFontSize,
        int listLevel,
        CodeBlockRenderer? codeBlockRenderer)
    {
        var list = new System.Windows.Documents.List
        {
            Margin = GetThickness("Markdown.List.Margin", MarkdownLayoutDefaults.ListMargin),
            Padding = new Thickness(
                block.IsOrdered
                    ? listLevel == 0 ? 20 : 16 + (listLevel - 1) * 8
                    : listLevel == 0 ? 20 : 12 + (listLevel - 1) * 8,
                0,
                0,
                0),
            MarkerOffset = listLevel == 0 ? 2 : 3,
            StartIndex = Math.Max(1, block.OrderedStart)
        };

        if (block.IsOrdered)
        {
            list.MarkerStyle = TextMarkerStyle.Decimal;
            list.FontSize = baseFontSize * 0.94;
        }
        else
        {
            list.MarkerStyle = listLevel switch
            {
                0 => TextMarkerStyle.Disc,
                1 => TextMarkerStyle.Circle,
                _ => TextMarkerStyle.Square
            };
            list.FontSize = baseFontSize * (listLevel switch
            {
                0 => 1.0,
                1 => 0.45,
                _ => 0.74
            });
        }

        foreach (var itemModel in block.Children)
        {
            if (itemModel.Kind != MarkdownBlockKind.ListItem)
            {
                continue;
            }

            var item = new ListItem
            {
                Margin = GetThickness("Markdown.List.Item.Margin", MarkdownLayoutDefaults.ListItemMargin),
                Padding = new Thickness(0)
            };

            foreach (var child in itemModel.Children)
            {
                var rendered = RenderBlock(child, fontFamily, baseFontSize, listLevel + 1, codeBlockRenderer);
                if (rendered is Paragraph paragraph)
                {
                    paragraph.Margin = new Thickness(0);
                    paragraph.FontSize = baseFontSize;
                    paragraph.LineHeight = baseFontSize * GetDouble("Markdown.LineHeight", MarkdownLayoutDefaults.LineHeightScale);
                }

                item.Blocks.Add(rendered);
            }

            list.ListItems.Add(item);
        }

        return list;
    }

    private System.Windows.Documents.Table RenderTable(
        MarkdownBlockModel block,
        FontFamily fontFamily,
        double baseFontSize,
        CodeBlockRenderer? codeBlockRenderer)
    {
        var table = new System.Windows.Documents.Table
        {
            CellSpacing = 0,
            Margin = GetThickness("Markdown.Table.Margin", MarkdownLayoutDefaults.TableMargin),
            BorderThickness = MarkdownLayoutDefaults.TableBorderThickness
        };
        SetDynamicResource(
            table,
            System.Windows.Documents.Table.BorderBrushProperty,
            "Markdown.Table.Border",
            new SolidColorBrush(Color.FromRgb(0xD5, 0xDE, 0xE9)));
        SetDynamicResource(
            table,
            System.Windows.Documents.Table.BackgroundProperty,
            "Markdown.Table.Background",
            Brushes.Transparent);

        var columnCount = GetTableColumnCount(block);
        for (var index = 0; index < columnCount; index++)
        {
            table.Columns.Add(new TableColumn());
        }

        var rowGroup = new TableRowGroup();
        foreach (var rowModel in block.Children)
        {
            if (rowModel.Kind != MarkdownBlockKind.TableRow)
            {
                continue;
            }

            var row = new TableRow();
            foreach (var cellModel in rowModel.Children)
            {
                if (cellModel.Kind != MarkdownBlockKind.TableCell)
                {
                    continue;
                }

                var cell = new TableCell
                {
                    Padding = GetThickness(
                        "Markdown.Table.Cell.Padding",
                        MarkdownLayoutDefaults.TableCellPadding),
                    BorderThickness = MarkdownLayoutDefaults.TableCellBorderThickness,
                    TextAlignment = GetTableTextAlignment(block, cellModel.ColumnIndex),
                    ColumnSpan = Math.Max(1, cellModel.ColumnSpan),
                    RowSpan = Math.Max(1, cellModel.RowSpan)
                };
                SetDynamicResource(
                    cell,
                    TableCell.BorderBrushProperty,
                    "Markdown.Table.Border",
                    new SolidColorBrush(Color.FromRgb(0xD5, 0xDE, 0xE9)));
                SetDynamicResource(
                    cell,
                    TableCell.ForegroundProperty,
                    "Markdown.Foreground",
                    new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)));

                if (rowModel.IsTableHeader)
                {
                    SetDynamicResource(
                        cell,
                        TableCell.BackgroundProperty,
                        "Markdown.Table.Header.Background",
                        new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF6)));
                    SetDynamicResource(
                        cell,
                        TableCell.ForegroundProperty,
                        "Markdown.Table.Header.Foreground",
                        new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)));
                    cell.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    SetDynamicResource(
                        cell,
                        TableCell.BackgroundProperty,
                        "Markdown.Table.Row.Background",
                        new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)));
                }

                foreach (var child in cellModel.Children)
                {
                    var rendered = RenderBlock(child, fontFamily, baseFontSize, 0, codeBlockRenderer);
                    if (rendered is Paragraph paragraph)
                    {
                        paragraph.Margin = new Thickness(0);
                        paragraph.LineHeight = baseFontSize * GetDouble("Markdown.LineHeight", MarkdownLayoutDefaults.LineHeightScale);
                    }

                    cell.Blocks.Add(rendered);
                }

                if (cell.Blocks.Count == 0)
                {
                    cell.Blocks.Add(new Paragraph(new Run(string.Empty)) { Margin = new Thickness(0) });
                }

                row.Cells.Add(cell);
            }

            if (row.Cells.Count > 0)
            {
                rowGroup.Rows.Add(row);
            }
        }

        if (rowGroup.Rows.Count > 0)
        {
            table.RowGroups.Add(rowGroup);
        }

        return table;
    }

    private static int GetTableColumnCount(MarkdownBlockModel block)
    {
        var maxColumns = 0;
        foreach (var row in block.Children.Where(child => child.Kind == MarkdownBlockKind.TableRow))
        {
            var rowColumns = row.Children
                .Where(child => child.Kind == MarkdownBlockKind.TableCell)
                .Sum(cell => Math.Max(1, cell.ColumnSpan));
            maxColumns = Math.Max(maxColumns, rowColumns);
        }

        return Math.Max(1, maxColumns == 0 ? block.TableColumnAlignments.Count : maxColumns);
    }

    private static TextAlignment GetTableTextAlignment(MarkdownBlockModel table, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= table.TableColumnAlignments.Count)
        {
            return TextAlignment.Left;
        }

        return table.TableColumnAlignments[columnIndex] switch
        {
            MarkdownTableColumnAlignment.Center => TextAlignment.Center,
            MarkdownTableColumnAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }

    private void AppendInlines(
        IReadOnlyList<MarkdownInlineModel> source,
        InlineCollection target,
        double baseFontSize)
    {
        foreach (var inline in source)
        {
            var converted = ConvertInline(inline, baseFontSize);
            if (converted != null)
            {
                target.Add(converted);
            }
        }
    }

    private Inline? ConvertInline(MarkdownInlineModel inline, double baseFontSize)
    {
        switch (inline.Kind)
        {
            case MarkdownInlineKind.Text:
                return new Run(inline.Text ?? inline.SourceText);
            case MarkdownInlineKind.Html:
                return new Run(inline.Text ?? inline.SourceText);
            case MarkdownInlineKind.LineBreak:
                return new LineBreak();
            case MarkdownInlineKind.Code:
                return ConvertInlineCode(inline, baseFontSize);
            case MarkdownInlineKind.Task:
                return ConvertTask(inline);
            case MarkdownInlineKind.Link:
                return ConvertLink(inline, baseFontSize);
            case MarkdownInlineKind.Autolink:
                return ConvertAutolink(inline);
            case MarkdownInlineKind.Image:
                return ConvertImage(inline);
            case MarkdownInlineKind.Emphasis:
            case MarkdownInlineKind.Strong:
            case MarkdownInlineKind.Strikethrough:
                var span = new Span();
                if (inline.Kind == MarkdownInlineKind.Emphasis)
                {
                    span.FontStyle = FontStyles.Italic;
                }
                else if (inline.Kind == MarkdownInlineKind.Strong)
                {
                    span.FontWeight = FontWeights.Bold;
                    SetDynamicResource(
                        span,
                        Span.ForegroundProperty,
                        "Markdown.Bold.Foreground",
                        new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
                }
                else
                {
                    span.TextDecorations = TextDecorations.Strikethrough;
                }

                AppendInlines(inline.Children, span.Inlines, baseFontSize);
                return span;
            default:
                return null;
        }
    }

    private static Inline ConvertInlineCode(MarkdownInlineModel inline, double baseFontSize)
    {
        var fontSize = baseFontSize * GetDouble(
            "Markdown.InlineCode.FontScale",
            MarkdownLayoutDefaults.InlineCodeFontScale);
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = inline.Text ?? inline.SourceText,
            FontFamily = GetFontFamily("Markdown.CodeFontFamily", "Consolas, Monaco, Courier New"),
            FontSize = fontSize,
            VerticalAlignment = VerticalAlignment.Center,
            LineHeight = fontSize * GetDouble(
                "Markdown.InlineCode.LineHeightScale",
                MarkdownLayoutDefaults.InlineCodeLineHeightScale),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };
        SetDynamicResource(
            textBlock,
            System.Windows.Controls.TextBlock.ForegroundProperty,
            "Markdown.InlineCode.Foreground",
            new SolidColorBrush(Color.FromRgb(0xF9, 0x82, 0x66)));

        var border = new Border
        {
            Child = textBlock,
            Padding = GetThickness("Markdown.InlineCode.Padding", MarkdownLayoutDefaults.InlineCodePadding),
            CornerRadius = GetCornerRadius("Markdown.InlineCode.CornerRadius", MarkdownLayoutDefaults.InlineCodeCornerRadius),
            BorderThickness = GetThickness("Markdown.InlineCode.BorderThickness", MarkdownLayoutDefaults.TableBorderThickness),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            MinHeight = Math.Ceiling(fontSize * GetDouble(
                "Markdown.InlineCode.MinHeightScale",
                MarkdownLayoutDefaults.InlineCodeMinHeightScale)),
            VerticalAlignment = VerticalAlignment.Center
        };
        SetDynamicResource(
            border,
            Border.BackgroundProperty,
            "Markdown.InlineCode.Background",
            new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E)));
        SetDynamicResource(
            border,
            Border.BorderBrushProperty,
            "Markdown.InlineCode.Border",
            new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));

        return new InlineUIContainer(border)
        {
            BaselineAlignment = BaselineAlignment.Center
        };
    }

    private static Inline ConvertTask(MarkdownInlineModel inline)
    {
        var checkBox = new CheckBox
        {
            IsChecked = inline.IsChecked,
            IsEnabled = false,
            IsHitTestVisible = false,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = GetThickness("Markdown.TaskList.Margin", MarkdownLayoutDefaults.TaskListMargin),
            MinWidth = 14,
            MinHeight = 14
        };
        System.Windows.Automation.AutomationProperties.SetName(
            checkBox,
            inline.IsChecked == true ? "Completed task" : "Incomplete task");
        return new InlineUIContainer(checkBox)
        {
            BaselineAlignment = BaselineAlignment.Center
        };
    }

    private Inline ConvertLink(MarkdownInlineModel inline, double baseFontSize)
    {
        if (inline.IsImage
            || _linkNavigator == null
            || !MarkdownRenderer.TryCreateSafeNavigateUri(inline.Url, out var uri))
        {
            var fallback = new Span();
            AppendInlines(inline.Children, fallback.Inlines, baseFontSize);
            if (fallback.Inlines.Count == 0)
            {
                fallback.Inlines.Add(new Run(inline.Text ?? inline.SourceText));
            }

            return fallback;
        }

        var hyperlink = new Hyperlink
        {
            NavigateUri = uri,
            TextDecorations = null,
            FontWeight = FontWeights.SemiBold
        };
        SetDynamicResource(
            hyperlink,
            Hyperlink.ForegroundProperty,
            "Markdown.Link.Foreground",
            new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
        hyperlink.RequestNavigate += (_, args) => _linkNavigator.Handle(args);
        AppendInlines(inline.Children, hyperlink.Inlines, baseFontSize);
        return hyperlink;
    }

    private Inline ConvertAutolink(MarkdownInlineModel inline)
    {
        var url = inline.Text ?? inline.Url ?? string.Empty;
        if (inline.IsEmail && !url.Contains(":", StringComparison.Ordinal))
        {
            url = "mailto:" + url;
        }

        if (_linkNavigator == null
            || !MarkdownRenderer.TryCreateSafeNavigateUri(url, out var uri))
        {
            return new Run(inline.Text ?? inline.SourceText);
        }

        var hyperlink = new Hyperlink
        {
            NavigateUri = uri,
            TextDecorations = null,
            FontWeight = FontWeights.SemiBold
        };
        SetDynamicResource(
            hyperlink,
            Hyperlink.ForegroundProperty,
            "Markdown.Link.Foreground",
            new SolidColorBrush(Color.FromRgb(0x4C, 0x63, 0xEB)));
        hyperlink.RequestNavigate += (_, args) => _linkNavigator.Handle(args);
        hyperlink.Inlines.Add(new Run(inline.Text ?? inline.SourceText));
        return hyperlink;
    }

    private Inline ConvertImage(MarkdownInlineModel inline)
    {
        if (_imageLoader == null || string.IsNullOrWhiteSpace(inline.Url))
        {
            return new Run("[图片加载失败]") { FontStyle = FontStyles.Italic };
        }

        if (!MarkdownRenderer.TryCreateSafeImageUri(inline.Url, out var imageUri))
        {
            return new Run("[图片已阻止：仅允许 http/https 地址]")
            {
                FontStyle = FontStyles.Italic
            };
        }

        var renderSession = _activeRenderSession;
        if (renderSession == null
            || !renderSession.TryReserveImage(_activeMaxImagesPerDocument))
        {
            return new Run("[图片数量超出限制]") { FontStyle = FontStyles.Italic };
        }

        var placeholder = new System.Windows.Controls.TextBlock
        {
            Text = "[图片加载中…]",
            FontStyle = FontStyles.Italic,
            Margin = GetThickness("Markdown.Image.Placeholder.Margin", MarkdownLayoutDefaults.ImagePlaceholderMargin)
        };
        var border = new Border
        {
            Child = placeholder,
            Background = GetBrush("Markdown.Image.Background", Colors.Transparent),
            BorderBrush = GetBrush("Markdown.Image.Border", Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderThickness = GetThickness("Markdown.Image.BorderThickness", MarkdownLayoutDefaults.ImageBorderThickness),
            CornerRadius = GetCornerRadius("Markdown.Image.CornerRadius", MarkdownLayoutDefaults.ImageCornerRadius),
            Padding = GetThickness("Markdown.Image.Padding", MarkdownLayoutDefaults.ImagePadding),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var tooltipText = inline.Children.FirstOrDefault()?.Text ?? inline.Children.FirstOrDefault()?.SourceText;
        if (!string.IsNullOrWhiteSpace(tooltipText))
        {
            border.ToolTip = new ToolTip
            {
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = tooltipText,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = GetDouble("Markdown.Image.TooltipMaxWidth", MarkdownLayoutDefaults.ImageTooltipMaxWidth)
                }
            };
        }

        _ = _imageLoader.LoadIntoAsync(
            border,
            imageUri!,
            placeholder,
            _activeImageLoadOptions,
            GetThickness("Markdown.Image.Margin", MarkdownLayoutDefaults.ImageMargin),
            renderSession.Cancellation.Token);

        return new InlineUIContainer(border);
    }

    private static void SetDynamicResource(
        FrameworkContentElement element,
        DependencyProperty property,
        string resourceKey,
        object defaultValue)
    {
        if (Application.Current?.TryFindResource(resourceKey) != null)
        {
            element.SetResourceReference(property, resourceKey);
        }
        else
        {
            element.SetValue(property, defaultValue);
        }
    }

    private static void SetDynamicResource(
        FrameworkElement element,
        DependencyProperty property,
        string resourceKey,
        object defaultValue)
    {
        if (Application.Current?.TryFindResource(resourceKey) != null)
        {
            element.SetResourceReference(property, resourceKey);
        }
        else
        {
            element.SetValue(property, defaultValue);
        }
    }

    private static double GetDouble(string key, double defaultValue)
        => Application.Current?.TryFindResource(key) is double value
            && value > 0
            && !double.IsNaN(value)
            && !double.IsInfinity(value)
            ? value
            : defaultValue;

    private static Thickness GetThickness(string key, Thickness defaultThickness)
        => Application.Current?.TryFindResource(key) is Thickness thickness
            ? thickness
            : defaultThickness;

    private static Brush GetBrush(string key, Color defaultColor)
        => Application.Current?.TryFindResource(key) is Brush brush
            ? brush
            : new SolidColorBrush(defaultColor);

    private static FontFamily GetFontFamily(string key, string defaultFont)
        => Application.Current?.TryFindResource(key) is FontFamily fontFamily
            ? fontFamily
            : new FontFamily(defaultFont);

    private static CornerRadius GetCornerRadius(string key, CornerRadius defaultCornerRadius)
        => Application.Current?.TryFindResource(key) is CornerRadius cornerRadius
            ? cornerRadius
            : defaultCornerRadius;
}
