using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Markdig;
using MarkdView.Enums;
using MarkdView.Interactions;
using MarkdView.Renderers;
using MarkdView.Services;
using Xunit;

namespace MarkdView.Tests.Interactions;

public class SideEffectPortTests
{
    [Fact]
    public void CodeBlockRenderer_ShouldUseInjectedClipboardAndHighlighter()
    {
        var result = RunInSta(() =>
        {
            var clipboard = new RecordingClipboard();
            var highlighter = new RecordingHighlighter();
            var renderer = new CodeBlockRenderer(
                enableSyntaxHighlighting: true,
                themeMode: ThemeMode.Dark,
                baseFontSize: 12,
                clipboardService: clipboard,
                syntaxHighlighter: highlighter);

            var block = renderer.Render("var value = 1;", "csharp");
            var outerGrid = Assert.IsType<Grid>(block.Child);
            var border = Assert.IsType<Border>(outerGrid.Children[0]);
            var containerGrid = Assert.IsType<Grid>(border.Child);
            var header = Assert.IsType<Border>(containerGrid.Children[0]);
            var headerGrid = Assert.IsType<Grid>(header.Child);
            var copyButton = Assert.IsType<Button>(headerGrid.Children[^1]);

            copyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            return (clipboard.Text, highlighter.CallCount);
        });

        Assert.Equal("var value = 1;", result.Text);
        Assert.Equal(1, result.CallCount);
    }

    [Fact]
    public void MarkdownRenderer_ShouldUseInjectedLinkHandler()
    {
        var result = RunInSta(() =>
        {
            var linkHandler = new RecordingLinkHandler();
            var renderer = new MarkdownRenderer(
                new MarkdownPipelineBuilder().UseAdvancedExtensions().Build(),
                linkHandler: linkHandler);
            var document = renderer.ConvertMarkdownToFlowDocument(
                "[docs](https://example.com/docs)",
                new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12)
                {
                    EnableSyntaxHighlighting = false
                });
            var paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
            var hyperlink = Assert.IsType<Hyperlink>(paragraph.Inlines.Single());
            hyperlink.RaiseEvent(new RequestNavigateEventArgs(hyperlink.NavigateUri!, string.Empty)
            {
                RoutedEvent = Hyperlink.RequestNavigateEvent
            });

            return linkHandler.Opened;
        });

        Assert.Equal(new Uri("https://example.com/docs"), result);
    }

    private sealed class RecordingClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public void SetText(string text) => Text = text;
    }

    private sealed class RecordingLinkHandler : IMarkdownLinkHandler
    {
        public Uri? Opened { get; private set; }

        public void Open(Uri uri) => Opened = uri;
    }

    private sealed class RecordingHighlighter : ISyntaxHighlighter
    {
        public int CallCount { get; private set; }

        public void ApplyHighlighting(TextBlock textBlock, string code, string? language)
            => CallCount++;
    }

    private static T RunInSta<T>(Func<T> func)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
        return result!;
    }
}
