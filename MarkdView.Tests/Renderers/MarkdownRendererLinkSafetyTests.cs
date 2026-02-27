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
        var ok = MarkdownRenderer.TryCreateSafeNavigateUri(url, out var uri);

        Assert.True(ok);
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
        var ok = MarkdownRenderer.TryCreateSafeNavigateUri(url, out var uri);

        Assert.False(ok);
        Assert.Null(uri);
    }
}
