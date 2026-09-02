using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using MarkdView.Media;
using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Media;

public class MarkdownImageLoaderPortTests
{
    [Fact]
    public void MarkdownRenderer_ShouldUseInjectedImageLoader()
    {
        var result = RunInSta(() =>
        {
            var loader = new RecordingImageLoader();
            var renderer = new MarkdownRenderer(
                new MarkdownPipelineBuilder().UseAdvancedExtensions().Build(),
                imageLoader: loader);
            var document = renderer.ConvertMarkdownToFlowDocument(
                "![diagram](https://example.com/diagram.png)",
                new MarkdownRenderOptions(new System.Windows.Media.FontFamily("Segoe UI"), 12));

            var paragraph = Assert.IsType<Paragraph>(document.Blocks.Single());
            var imageContainer = Assert.IsType<InlineUIContainer>(paragraph.Inlines.Single());
            var border = Assert.IsType<Border>(imageContainer.Child);
            return (loader.CallCount, border.Child);
        });

        Assert.Equal(1, result.CallCount);
        Assert.IsType<Image>(result.Child);
    }

    [Fact]
    public void WpfImageAdapter_ShouldExposeFailureInPlaceholder()
    {
        var result = RunInSta(() =>
        {
            var adapter = new WpfMarkdownImageLoader(new ThrowingImageLoader());
            var placeholder = new TextBlock { Text = "[图片加载中...]" };
            var border = new Border { Child = placeholder };
            adapter.LoadIntoAsync(
                    border,
                    new Uri("https://example.com/failure.png"),
                    placeholder,
                    new MarkdownImageLoadOptions(TimeSpan.FromSeconds(1), 1024),
                    new Thickness(0, 4, 0, 4),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return placeholder.Text;
        });

        Assert.Equal("[图片加载失败: https://example.com/failure.png]", result);
    }

    [Fact]
    public void WpfImageAdapter_ShouldKeepPlaceholderWhenCancelled()
    {
        var result = RunInSta(() =>
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var adapter = new WpfMarkdownImageLoader(new CancelledImageLoader());
            var placeholder = new TextBlock { Text = "[图片加载中...]" };
            var border = new Border { Child = placeholder };
            adapter.LoadIntoAsync(
                    border,
                    new Uri("https://example.com/cancelled.png"),
                    placeholder,
                    new MarkdownImageLoadOptions(TimeSpan.FromSeconds(1), 1024),
                    new Thickness(0, 4, 0, 4),
                    cancellation.Token)
                .GetAwaiter()
                .GetResult();
            return (placeholder.Text, border.Child);
        });

        Assert.Equal("[图片加载中...]", result.Text);
        Assert.IsType<TextBlock>(result.Child);
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

    private sealed class ThrowingImageLoader : IMarkdownImageLoader
    {
        public Task<BitmapSource> LoadAsync(Uri uri, MarkdownImageLoadOptions options, CancellationToken cancellationToken)
            => Task.FromException<BitmapSource>(new InvalidOperationException("test failure"));
    }

    private sealed class CancelledImageLoader : IMarkdownImageLoader
    {
        public Task<BitmapSource> LoadAsync(Uri uri, MarkdownImageLoadOptions options, CancellationToken cancellationToken)
            => Task.FromCanceled<BitmapSource>(cancellationToken);
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
