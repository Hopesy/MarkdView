using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Renderers;

public class MarkdownRendererLinkSafetyTests
{
    [Theory]
    [InlineData("https://learn.microsoft.com")]
    [InlineData("http://example.com/docs")]
    [InlineData("mailto:support@example.com")]
    public void TryCreateSafeNavigateUri_Allows_ExpectedSchemes(string url)
    {
        Assert.True(MarkdownRenderer.TryCreateSafeNavigateUri(url, out var uri));
        Assert.NotNull(uri);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:display")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("/relative/path")]
    [InlineData("")]
    public void TryCreateSafeNavigateUri_Rejects_UnsafeOrInvalidUri(string url)
    {
        Assert.False(MarkdownRenderer.TryCreateSafeNavigateUri(url, out var uri));
        Assert.Null(uri);
    }

    [Theory]
    [InlineData("https://example.com/image.png")]
    [InlineData("http://example.com/image.png")]
    public void TryCreateSafeImageUri_AllowsHttp(string url)
        => Assert.True(MarkdownRenderer.TryCreateSafeImageUri(url, out _));

    [Theory]
    [InlineData("file:///C:/secret.png")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("C:\\secret.png")]
    [InlineData("https://127.0.0.1/image.png")]
    [InlineData("https://[::1]/image.png")]
    [InlineData("https://[::ffff:127.0.0.1]/image.png")]
    [InlineData("https://100.64.0.1/image.png")]
    [InlineData("https://user:password@example.com/image.png")]
    public void TryCreateSafeImageUri_RejectsLocalAndData(string url)
        => Assert.False(MarkdownRenderer.TryCreateSafeImageUri(url, out _));

    [Fact]
    public void TryCreateSafeNavigateUri_RejectsEmbeddedCredentials()
    {
        Assert.False(MarkdownRenderer.TryCreateSafeNavigateUri(
            "https://user:password@example.com/docs",
            out var uri));
        Assert.Null(uri);
    }

    [Fact]
    public void TryCreateSafeNavigateUri_RejectsOversizedUri()
    {
        var url = "https://example.com/" + new string('a', 4096);
        Assert.False(MarkdownRenderer.TryCreateSafeNavigateUri(url, out var uri));
        Assert.Null(uri);
    }
}
