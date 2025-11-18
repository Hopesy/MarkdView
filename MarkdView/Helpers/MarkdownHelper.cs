using System.Windows;
using MarkdView.Controls;

namespace MarkdView.Helpers;

/// <summary>
/// MarkdownViewer 附加属性辅助类
/// 用于在 DataTemplate 中正确设置 Markdown 内容
/// </summary>
public static class MarkdownHelper
{
    /// <summary>
    /// Markdown 附加属性
    /// 用于在 XAML 中设置 MarkdownViewer 的 Markdown 内容
    /// </summary>
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.RegisterAttached(
            "Markdown",
            typeof(string),
            typeof(MarkdownHelper),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static string GetMarkdown(DependencyObject obj)
    {
        return (string)obj.GetValue(MarkdownProperty);
    }

    public static void SetMarkdown(DependencyObject obj, string value)
    {
        obj.SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer)
        {
            var newMarkdown = e.NewValue as string ?? string.Empty;
            System.Console.WriteLine($"[MarkdownHelper] Attached property changed: length={newMarkdown.Length}");
            viewer.Markdown = newMarkdown;
        }
    }
}
