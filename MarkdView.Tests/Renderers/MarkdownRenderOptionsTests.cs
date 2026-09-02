using System;
using Markdig;
using System.Windows.Media;
using MarkdView.Enums;
using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Renderers;

public sealed class MarkdownRenderOptionsTests
{
    [Fact]
    public void PublicCompatibilitySignatures_ShouldRemainAvailable()
    {
        Assert.NotNull(typeof(MarkdownRenderer).GetConstructor(new[] { typeof(MarkdownPipeline) }));
        Assert.NotNull(typeof(CodeBlockRenderer).GetConstructor(new[]
        {
            typeof(bool),
            typeof(ThemeMode),
            typeof(double)
        }));
        Assert.NotNull(typeof(MarkdownRenderer).GetMethod(
            nameof(MarkdownRenderer.ConvertMarkdownToFlowDocument),
            new[]
            {
                typeof(string),
                typeof(FontFamily),
                typeof(double),
                typeof(bool),
                typeof(CodeBlockRenderer),
                typeof(bool)
            }));
    }

    [Fact]
    public void MaxImagesPerDocument_ShouldRejectNegativeValuesAtConfigurationBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12)
            {
                MaxImagesPerDocument = -1
            });
    }

    [Fact]
    public void MaxImagesPerDocument_ShouldAcceptZeroAsDisableAllImages()
    {
        var options = new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12)
        {
            MaxImagesPerDocument = 0
        };

        Assert.Equal(0, options.MaxImagesPerDocument);
    }

    [Fact]
    public void MaxImagesPerDocument_ShouldRejectValuesAboveSafetyLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MarkdownRenderOptions(new FontFamily("Segoe UI"), 12)
            {
                MaxImagesPerDocument = MarkdownRenderDefaults.MaxImagesPerDocumentLimit + 1
            });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void CodeBlockRenderer_ShouldRejectInvalidBaseFontSize(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CodeBlockRenderer(false, ThemeMode.Light, value));
    }

    [Fact]
    public void CodeBlockRenderer_ShouldRejectUnknownTheme()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CodeBlockRenderer(false, (ThemeMode)99, 12));
    }
}
