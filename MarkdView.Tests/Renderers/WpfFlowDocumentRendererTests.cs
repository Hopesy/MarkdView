using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Markdig;
using MarkdView.Documents;
using MarkdView.Enums;
using MarkdView.Interactions;
using MarkdView.Media;
using MarkdView.Parsing;
using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Renderers;

public class WpfFlowDocumentRendererTests
{
    [Fact]
    public void ModelAdapter_ShouldRenderBasicBlocksAndInlineStyles()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("# Title\n\n**bold** and ~~deleted~~");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            Assert.Equal(new Thickness(12, 6, 12, 8), document.PagePadding);
            var heading = Assert.IsType<Paragraph>(document.Blocks.First());
            Assert.Equal(new Thickness(0, 10, 0, 6), heading.Margin);
            Assert.Equal(Color.FromRgb(0x0F, 0x17, 0x2A), Assert.IsType<SolidColorBrush>(heading.Foreground).Color);

            var paragraph = Assert.IsType<Paragraph>(document.Blocks.Skip(1).First());
            Assert.Contains(paragraph.Inlines, inline =>
                inline is Span span && span.FontWeight == FontWeights.Bold);
            Assert.Contains(paragraph.Inlines, inline =>
                inline is Span span && span.TextDecorations != null);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderTableFromModel()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("| Name |\n| --- |\n| value |");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var table = Assert.IsType<Table>(Assert.Single(document.Blocks));
            Assert.Single(table.Columns);
            var rowGroup = Assert.Single(table.RowGroups);
            Assert.Equal(2, rowGroup.Rows.Count);
            var header = Assert.Single(rowGroup.Rows.First().Cells);
            Assert.Equal(FontWeights.SemiBold, header.FontWeight);
            Assert.Equal(new Thickness(5, 2, 5, 2), header.Padding);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldPreserveTableColumnAlignmentAndSpans()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel(
                "| Name | Score |\n| :--- | ---: |\n| Alice | 95 |\n| Bob | 88 |");

            var tableModel = Assert.Single(model.Blocks, block => block.Kind == MarkdownBlockKind.Table);
            Assert.Equal(MarkdownTableColumnAlignment.Left, tableModel.TableColumnAlignments[0]);
            Assert.Equal(MarkdownTableColumnAlignment.Right, tableModel.TableColumnAlignments[1]);

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));
            var table = Assert.IsType<Table>(Assert.Single(document.Blocks));
            var rows = Assert.Single(table.RowGroups).Rows.Cast<TableRow>().ToArray();
            var headerCells = rows[0].Cells.Cast<TableCell>().ToArray();
            Assert.Equal(TextAlignment.Left, headerCells[0].TextAlignment);
            Assert.Equal(TextAlignment.Right, headerCells[1].TextAlignment);
            Assert.Equal(2, rows[1].Cells.Count);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderQuoteFromModel()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("> quoted\n>\n> **important**");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var quote = Assert.IsType<Section>(Assert.Single(document.Blocks));
            Assert.Equal(new Thickness(0, 5, 0, 7), quote.Margin);
            Assert.Equal(new Thickness(3, 0, 0, 0), quote.BorderThickness);
            Assert.Equal(new Thickness(8, 6, 8, 6), quote.Padding);
            Assert.Contains(quote.Blocks, block => block is Paragraph);
            Assert.Contains(
                quote.Blocks.OfType<Paragraph>(),
                paragraph => paragraph.Inlines.Any(inline =>
                    inline is Span span && span.FontWeight == FontWeights.Bold));
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderNestedListsAndOrderedStartFromModel()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("3. first\n4. second\n   - nested");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var list = Assert.IsType<List>(Assert.Single(document.Blocks));
            Assert.Equal(TextMarkerStyle.Decimal, list.MarkerStyle);
            Assert.Equal(3, list.StartIndex);
            Assert.Equal(2, list.ListItems.Count);
            var items = list.ListItems.Cast<ListItem>().ToArray();
            Assert.Contains(items[1].Blocks, block => block is List);
            var nested = Assert.IsType<List>(items[1].Blocks.Last());
            Assert.Equal(TextMarkerStyle.Circle, nested.MarkerStyle);
            Assert.Single(nested.ListItems);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderInlineCodeAndTaskStateFromModel()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(
                new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTaskLists().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("Use `code` here.\n\n- [x] done");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 16));

            var paragraph = Assert.IsType<Paragraph>(document.Blocks.First());
            var code = Assert.IsType<InlineUIContainer>(
                paragraph.Inlines.First(inline => inline is InlineUIContainer));
            var codeBorder = Assert.IsType<System.Windows.Controls.Border>(code.Child);
            var codeText = Assert.IsType<System.Windows.Controls.TextBlock>(codeBorder.Child);
            Assert.Equal(16 * 0.88, codeText.FontSize);

            var list = Assert.IsType<List>(document.Blocks.Skip(1).First());
            var itemParagraph = Assert.IsType<Paragraph>(list.ListItems.Cast<ListItem>().Single().Blocks.Single());
            var task = Assert.IsType<InlineUIContainer>(
                itemParagraph.Inlines.First(inline => inline is InlineUIContainer));
            var checkBox = Assert.IsType<System.Windows.Controls.CheckBox>(task.Child);
            Assert.True(checkBox.IsChecked);
            Assert.False(checkBox.IsEnabled);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderThematicBreakFromModel()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("before\n\n---\n\nafter");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var separator = Assert.IsType<Paragraph>(document.Blocks.Skip(1).First());
            Assert.Equal(new Thickness(0, 6, 0, 6), separator.Margin);
            Assert.Equal(new Thickness(0, 1, 0, 0), separator.BorderThickness);
            Assert.Equal(
                Color.FromRgb(0xD5, 0xDE, 0xE9),
                Assert.IsType<SolidColorBrush>(separator.BorderBrush).Color);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldFallbackUnsupportedTopLevelBlocksIndividually()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel(
                "# before\n\n<div>legacy html</div>\n\n## after");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var blocks = document.Blocks.ToArray();
            Assert.Equal(3, blocks.Length);
            Assert.Equal(1.75 * 12, Assert.IsType<Paragraph>(blocks[0]).FontSize);
            Assert.Equal("<div>legacy html</div>",
                Assert.IsType<Paragraph>(blocks[1]).Inlines.OfType<Run>().Single().Text);
            Assert.Equal(1.42 * 12, Assert.IsType<Paragraph>(blocks[2]).FontSize);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderSafeLinksAndRouteNavigationThroughHandler()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var linkHandler = new RecordingLinkHandler();
            var legacy = new MarkdownRenderer(
                new MarkdownPipelineBuilder().UseAdvancedExtensions().Build(),
                linkHandler: linkHandler);
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel(
                "[docs](https://example.com/docs) and <mailto:user@example.com>");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
            var links = paragraph.Inlines.OfType<Hyperlink>().ToArray();
            Assert.Equal(2, links.Length);
            Assert.Equal(new Uri("https://example.com/docs"), links[0].NavigateUri);
            Assert.Equal(new Uri("mailto:user@example.com"), links[1].NavigateUri);

            links[0].RaiseEvent(new RequestNavigateEventArgs(
                links[0].NavigateUri!,
                string.Empty)
            {
                RoutedEvent = Hyperlink.RequestNavigateEvent
            });
            Assert.Equal(new Uri("https://example.com/docs"), linkHandler.Opened);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderUnsafeLinksAsText()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("[unsafe](javascript:alert(1))");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
            Assert.DoesNotContain(paragraph.Inlines, inline => inline is Hyperlink);
            Assert.Contains(
                paragraph.Inlines,
                inline => inline is Span span
                    && span.Inlines.OfType<Run>().Any(run => run.Text == "unsafe"));
        });
    }

    [Fact]
    public void ModelAdapter_ShouldLoadSafeImagesThroughInjectedPort()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var imageLoader = new RecordingImageLoader();
            var legacy = new MarkdownRenderer(
                new MarkdownPipelineBuilder().UseAdvancedExtensions().Build(),
                imageLoader: imageLoader);
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("![diagram](https://example.com/diagram.png)");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks));
            var imageContainer = Assert.IsType<InlineUIContainer>(paragraph.Inlines.Single());
            var border = Assert.IsType<System.Windows.Controls.Border>(imageContainer.Child);
            Assert.IsType<System.Windows.Controls.Image>(border.Child);
            Assert.Equal(1, imageLoader.CallCount);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldShareImageBudgetAcrossModelAndCompatibilityFallback()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var imageLoader = new RecordingImageLoader();
            var legacy = new MarkdownRenderer(
                MarkdownPipelineFactory.CreateDefault(),
                imageLoader: imageLoader);
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel(
                "![first](https://example.com/first.png)\n\nTerm\n:   definition ![second](https://example.com/second.png)");

            _ = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12)
                {
                    MaxImagesPerDocument = 1
                });

            Assert.Equal(1, imageLoader.CallCount);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldRenderCodeBlocksThroughConfiguredRenderer()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("```csharp\nvar value = 1;\n```");
            var codeRenderer = new CodeBlockRenderer(enableSyntaxHighlighting: false, baseFontSize: 12);

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12)
                {
                    EnableSyntaxHighlighting = false,
                    CodeBlockRenderer = codeRenderer
                });

            var codeBlock = Assert.IsType<BlockUIContainer>(Assert.Single(document.Blocks));
            var outerGrid = Assert.IsType<System.Windows.Controls.Grid>(codeBlock.Child);
            Assert.Equal(new Thickness(0, 4, 0, 6), outerGrid.Margin);
            Assert.Single(outerGrid.Children);
            var border = Assert.IsType<System.Windows.Controls.Border>(outerGrid.Children[0]);
            var containerGrid = Assert.IsType<System.Windows.Controls.Grid>(border.Child);
            var codeScrollViewer = Assert.IsType<System.Windows.Controls.ScrollViewer>(containerGrid.Children[1]);
            Assert.Equal(480d, codeScrollViewer.MaxHeight);
        });
    }

    [Fact]
    public void ModelAdapter_ShouldFallbackCodeBlockWhenNoRendererIsConfigured()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            var legacy = new MarkdownRenderer(new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            var adapter = new WpfFlowDocumentRenderer(legacy);
            var model = legacy.ParseDocumentModel("before\n\n```text\nlegacy\n```\n\nafter");

            var document = adapter.ConvertDocumentToFlowDocument(
                model,
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12));

            Assert.Equal(3, document.Blocks.Count);
            Assert.IsType<Paragraph>(document.Blocks.First());
            Assert.IsType<BlockUIContainer>(document.Blocks.Skip(1).First());
            Assert.IsType<Paragraph>(document.Blocks.Last());
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
        {
            throw error;
        }
    }

    private sealed class RecordingLinkHandler : IMarkdownLinkHandler
    {
        public Uri? Opened { get; private set; }

        public void Open(Uri uri) => Opened = uri;
    }

    private sealed class RecordingImageLoader : IMarkdownImageLoader
    {
        public int CallCount { get; private set; }

        public Task<BitmapSource> LoadAsync(
            Uri uri,
            MarkdownImageLoadOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var bitmap = new BitmapImage();
            bitmap.Freeze();
            return Task.FromResult<BitmapSource>(bitmap);
        }
    }
}
