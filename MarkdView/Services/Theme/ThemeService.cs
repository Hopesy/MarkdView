using System.Windows;
using System.Windows.Media;

namespace MarkdView.Services.Theme;

/// Markdown 主题服务 - 负责管理主题资源
// 暂时没有使用
public class ThemeService
{
    /// <summary>
    /// 应用指定主题
    /// </summary>
    public void ApplyTheme(ThemeMode theme)
    {
        switch (theme)
        {
            case ThemeMode.Light:
                ApplyLightTheme();
                break;
            case ThemeMode.Dark:
                ApplyDarkTheme();
                break;
        }
    }

    /// <summary>
    /// 应用浅色主题
    /// </summary>
    public void ApplyLightTheme()
    {
        var resources = Application.Current.Resources;

        // 清除现有主题资源
        RemoveThemeResources();

        // 应用浅色主题
        resources["Markdown.Foreground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        resources["Markdown.Background"] = new SolidColorBrush(Colors.White);

        // 标题
        resources["Markdown.Heading.H1.Foreground"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        resources["Markdown.Heading.H1.Border"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        resources["Markdown.Heading.H2.Foreground"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        resources["Markdown.Heading.H2.Border"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        // 引用块
        resources["Markdown.Quote.Background"] = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9));
        resources["Markdown.Quote.Border"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x9D, 0xFF));

        // 内联代码
        resources["Markdown.InlineCode.Background"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        resources["Markdown.InlineCode.Foreground"] = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

        // 链接
        resources["Markdown.Link.Foreground"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x9D, 0xFF));

        // 代码块
        resources["Markdown.CodeBlock.Background"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        resources["Markdown.CodeBlock.Border"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
        resources["Markdown.CodeBlock.Header.Background"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        resources["Markdown.CodeBlock.Language.Foreground"] = new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50));
        resources["Markdown.CodeBlock.CopyButton.Background"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xD8));
        resources["Markdown.CodeBlock.CopyButton.Foreground"] = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
        resources["Markdown.CodeBlock.Foreground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    }

    /// <summary>
    /// 应用深色主题
    /// </summary>
    public void ApplyDarkTheme()
    {
        var resources = Application.Current.Resources;

        // 清除现有主题资源
        RemoveThemeResources();

        // 应用深色主题
        resources["Markdown.Foreground"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        resources["Markdown.Background"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        // 标题
        resources["Markdown.Heading.H1.Foreground"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        resources["Markdown.Heading.H1.Border"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        resources["Markdown.Heading.H2.Foreground"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        resources["Markdown.Heading.H2.Border"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));

        // 引用块
        resources["Markdown.Quote.Background"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
        resources["Markdown.Quote.Border"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));

        // 内联代码
        resources["Markdown.InlineCode.Background"] = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
        resources["Markdown.InlineCode.Foreground"] = new SolidColorBrush(Color.FromRgb(0xF9, 0x82, 0x66));

        // 链接
        resources["Markdown.Link.Foreground"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));

        // 代码块
        resources["Markdown.CodeBlock.Background"] = new SolidColorBrush(Color.FromRgb(0x28, 0x2C, 0x34));
        resources["Markdown.CodeBlock.Border"] = new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x2B));
        resources["Markdown.CodeBlock.Header.Background"] = new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x2B));
        resources["Markdown.CodeBlock.Language.Foreground"] = new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF));
        resources["Markdown.CodeBlock.CopyButton.Background"] = new SolidColorBrush(Color.FromRgb(0x3C, 0x40, 0x48));
        resources["Markdown.CodeBlock.CopyButton.Foreground"] = new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF));
        resources["Markdown.CodeBlock.Foreground"] = new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF));
    }

    /// <summary>
    /// 清除主题资源
    /// </summary>
    public void RemoveThemeResources()
    {
        var resources = Application.Current.Resources;

        // 移除主题相关资源
        var keysToRemove = new[]
        {
            "Markdown.Foreground",
            "Markdown.Background",
            "Markdown.Heading.H1.Foreground",
            "Markdown.Heading.H1.Border",
            "Markdown.Heading.H2.Foreground",
            "Markdown.Heading.H2.Border",
            "Markdown.Quote.Background",
            "Markdown.Quote.Border",
            "Markdown.InlineCode.Background",
            "Markdown.InlineCode.Foreground",
            "Markdown.Link.Foreground",
            "Markdown.CodeBlock.Background",
            "Markdown.CodeBlock.Border",
            "Markdown.CodeBlock.Header.Background",
            "Markdown.CodeBlock.Language.Foreground",
            "Markdown.CodeBlock.CopyButton.Background",
            "Markdown.CodeBlock.CopyButton.Foreground",
            "Markdown.CodeBlock.Foreground"
        };

        foreach (var key in keysToRemove)
        {
            if (resources.Contains(key))
            {
                resources.Remove(key);
            }
        }
    }
}
