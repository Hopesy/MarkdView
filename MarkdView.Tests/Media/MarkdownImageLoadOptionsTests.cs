using System;
using System.Threading;
using System.Threading.Tasks;
using MarkdView.Media;
using Xunit;

namespace MarkdView.Tests.Media;

public class MarkdownImageLoadOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    public void MaxDecodePixel_ShouldRejectUnsafeValues(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MarkdownImageLoadOptions(TimeSpan.FromSeconds(1), 1024)
            {
                MaxDecodePixel = value
            });
    }

    [Fact]
    public void Constructor_ShouldRejectUnsafeTimeoutAndSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MarkdownImageLoadOptions(MarkdownImageLoadOptions.MaxTimeout + TimeSpan.FromMilliseconds(1), 1024));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MarkdownImageLoadOptions(TimeSpan.FromSeconds(1), MarkdownImageLoadOptions.MaxAllowedBytes + 1));
    }

    [Fact]
    public void MaxDecodePixel_ShouldAcceptConfiguredValue()
    {
        var options = new MarkdownImageLoadOptions(TimeSpan.FromSeconds(1), 1024)
        {
            MaxDecodePixel = 2048
        };

        Assert.Equal(2048, options.MaxDecodePixel);
    }

    [Fact]
    public void MaxDecodePixel_ShouldDefaultToSafeBound()
    {
        var options = new MarkdownImageLoadOptions(TimeSpan.FromSeconds(1), 1024);

        Assert.Equal(MarkdownImageLoadOptions.DefaultMaxDecodePixel, options.MaxDecodePixel);
    }

    [Fact]
    public async Task EnsurePublicEndpointAsync_ShouldPreserveCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            MarkdownImageSecurity.EnsurePublicEndpointAsync(
                new Uri("https://example.com/image.png"),
                cancellation.Token));
    }
}
