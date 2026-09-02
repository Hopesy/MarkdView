using System;
using System.Linq;
using System.Threading;
using System.Windows;
using MarkdView;
using MarkdView.Controls;
using MarkdView.Enums;
using MarkdView.Media;
using System.Windows.Media;
using Xunit;

namespace MarkdView.Tests;

public class ThemeAndControlTests
{
    [Fact]
    public void ThemeDictionaries_LoadAndSwitchWithoutLosingState()
    {
        RunInSta(() =>
        {
            _ = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);
            Assert.Equal(ThemeMode.Light, ThemeManager.CurrentTheme);
            Assert.Equal(Color.FromRgb(0xF8, 0xFA, 0xFC), ((SolidColorBrush)Application.Current!.TryFindResource("Markdown.Background")!).Color);
            Assert.Equal(Color.FromRgb(0xE8, 0xEE, 0xF6), ((SolidColorBrush)Application.Current.TryFindResource("Markdown.Toolbar.Background")!).Color);
            Assert.Equal(new Thickness(0, 4, 0, 6), (Thickness)Application.Current.TryFindResource("Markdown.CodeBlock.Margin")!);
            Assert.Equal(new Thickness(8, 4, 8, 4), (Thickness)Application.Current.TryFindResource("Markdown.CodeBlock.Content.Padding")!);
            Assert.Equal(480d, Assert.IsType<double>(Application.Current.TryFindResource("Markdown.CodeBlock.Content.MaxHeight")));
            Assert.Equal(new Thickness(0, 1, 0, 4), (Thickness)Application.Current.TryFindResource("Markdown.Paragraph.Margin")!);
            Assert.NotNull(Application.Current.TryFindResource("Markdown.Error.Foreground"));
            Assert.Equal(800d, Assert.IsType<double>(Application.Current.TryFindResource("Markdown.Image.MaxWidth")));

            ThemeManager.ApplyTheme(ThemeMode.Dark);
            Assert.Equal(ThemeMode.Dark, ThemeManager.CurrentTheme);
            Assert.Equal(Color.FromRgb(0x1A, 0x1B, 0x1D), ((SolidColorBrush)Application.Current.TryFindResource("Markdown.Background")!).Color);
            Assert.Equal(Color.FromRgb(0x16, 0x1B, 0x22), ((SolidColorBrush)Application.Current.TryFindResource("Markdown.Toolbar.Background")!).Color);
            Assert.Equal(new Thickness(0, 4, 0, 6), (Thickness)Application.Current.TryFindResource("Markdown.CodeBlock.Margin")!);
            Assert.NotNull(Application.Current.TryFindResource("Markdown.Error.Foreground"));
        });
    }

    [Fact]
    public void ThemeManager_ShouldHandleApplicationCreatedOnPreviousStaThread()
    {
        RunInSta(() => _ = Application.Current ?? new Application());

        RunInSta(() =>
        {
            ThemeManager.ApplyTheme(ThemeMode.Light);
            Assert.Equal(ThemeMode.Light, ThemeManager.CurrentTheme);
            var background = Assert.IsType<SolidColorBrush>(
                Application.Current!.TryFindResource("Markdown.Background"));
            Assert.Equal(Color.FromRgb(0xF8, 0xFA, 0xFC), background.Color);
        });
    }

    [Fact]
    public void ThemeManager_ShouldUseLastDuplicateThemeDictionaryAsEffectiveTheme()
    {
        RunInSta(() =>
        {
            var app = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Light);

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/MarkdView;component/Themes/MarkdView.Dark.xaml",
                    UriKind.Absolute)
            });

            ThemeManager.EnsureThemeApplied();

            Assert.Equal(ThemeMode.Dark, ThemeManager.CurrentTheme);
            Assert.Single(app.Resources.MergedDictionaries, dictionary =>
                dictionary.Source?.ToString().Contains(
                    "MarkdView.Dark.xaml",
                    StringComparison.OrdinalIgnoreCase) == true);
            Assert.Equal(
                Color.FromRgb(0x1A, 0x1B, 0x1D),
                ((SolidColorBrush)app.TryFindResource("Markdown.Background")!).Color);
        });
    }

    [Fact]
    public void ThemeManager_ShouldPreserveHostDictionaryPrecedenceWhenReplacingTheme()
    {
        RunInSta(() =>
        {
            var app = Application.Current ?? new Application();
            ThemeManager.ApplyTheme(ThemeMode.Dark);

            var hostBrush = new SolidColorBrush(Color.FromRgb(0x12, 0x34, 0x56));
            var hostResources = new ResourceDictionary();
            hostResources["Markdown.Background"] = hostBrush;
            app.Resources.MergedDictionaries.Add(hostResources);

            try
            {
                ThemeManager.ApplyTheme(ThemeMode.Light);
                Assert.Same(hostBrush, app.TryFindResource("Markdown.Background"));
            }
            finally
            {
                app.Resources.MergedDictionaries.Remove(hostResources);
                ThemeManager.ApplyTheme(ThemeMode.Dark);
            }
        });
    }

    [Fact]
    public void PublicProperties_RejectInvalidValues()
    {
        RunInSta(() =>
        {
            var viewer = new MarkdownViewer();
            Assert.Throws<ArgumentException>(() => viewer.StreamingThrottle = 0);
            Assert.Throws<ArgumentException>(() => viewer.FontSize = -1);
            Assert.Throws<ArgumentException>(() => viewer.ImageLoadTimeout = TimeSpan.Zero);
            Assert.Throws<ArgumentException>(() => viewer.ImageLoadTimeout = MarkdownImageLoadOptions.MaxTimeout + TimeSpan.FromMilliseconds(1));
            Assert.Throws<ArgumentException>(() => viewer.MaxImageBytes = 0);
            Assert.Throws<ArgumentException>(() => viewer.MaxImageBytes = MarkdownImageLoadOptions.MaxAllowedBytes + 1);
            Assert.Throws<ArgumentException>(() => viewer.MaxImagesPerDocument = -1);
            Assert.Throws<ArgumentException>(() => viewer.MaxImageDecodePixel = 0);
            viewer.FontSize = 16;
            viewer.ImageLoadTimeout = TimeSpan.FromSeconds(5);
            viewer.MaxImageBytes = 1024 * 1024;
            viewer.MaxImagesPerDocument = 0;
            viewer.MaxImageDecodePixel = 2048;
            Assert.Equal(16, viewer.FontSize);
            Assert.Equal(TimeSpan.FromSeconds(5), viewer.ImageLoadTimeout);
            Assert.Equal(1024 * 1024, viewer.MaxImageBytes);
            Assert.Equal(0, viewer.MaxImagesPerDocument);
            Assert.Equal(2048, viewer.MaxImageDecodePixel);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }
}
