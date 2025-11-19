using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MarkdView.Services;

/// <summary>
/// 代码语法高亮器
/// </summary>
public static class SyntaxHighlighter
{
    /// <summary>
    /// 对 TextBlock 应用语法高亮
    /// </summary>
    public static void ApplyHighlighting(System.Windows.Controls.TextBlock textBlock, string code, string? language, bool isLightTheme = false)
    {
        // 使用正确的行分隔符分割，并去掉空行
        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var colorScheme = isLightTheme ? (IColorScheme)new LightColorScheme() : new DarkColorScheme();

        for (int i = 0; i < lines.Length; i++)
        {
            ApplyLineHighlighting(textBlock, lines[i], colorScheme);

            // 只在非最后一行时添加换行
            if (i < lines.Length - 1)
            {
                textBlock.Inlines.Add(new LineBreak());
            }
        }
    }

    /// <summary>
    /// 对单行应用语法高亮
    /// </summary>
    private static void ApplyLineHighlighting(System.Windows.Controls.TextBlock textBlock, string line, IColorScheme colorScheme)
    {
        // 专业语法高亮 - 按优先级处理（注释 > 字符串 > 特性 > 关键字 > 类型 > 数字 > 函数）

        // 1. 注释（最高优先级，注释内的内容不再处理）
        var commentPattern = @"//.*$|/\*[\s\S]*?\*/|#.*$";
        // 2. 字符串（第二优先级）
        var stringPattern = @"""([^""\\]|\\.)*""|'([^'\\]|\\.)*'|`[^`]*`";
        // 3. C# 特性/装饰器
        var attributePattern = @"\[[\w\s,()=\[\]]+\]";
        // 4. 控制流关键字（紫色）
        var controlKeywords = @"\b(if|else|switch|case|default|for|foreach|while|do|break|continue|return|throw|try|catch|finally|yield|await|async)\b";
        // 5. 声明关键字（蓝色）
        var declarationKeywords = @"\b(class|interface|struct|enum|namespace|using|public|private|protected|internal|static|readonly|const|var|new|this|base|abstract|virtual|override|sealed|partial|delegate|event)\b";
        // 6. 类型关键字（青色）
        var typeKeywords = @"\b(void|int|long|short|byte|sbyte|uint|ulong|ushort|bool|char|string|float|double|decimal|object|dynamic)\b";
        // 7. 字面量（蓝色）
        var literalKeywords = @"\b(true|false|null)\b";
        // 8. 泛型和类型（青色）
        var typePattern = @"\b[A-Z][a-zA-Z0-9]*(?:<[^>]+>)?(?=\s|\(|<|,|;|\[|\])";
        // 9. 数字
        var numberPattern = @"\b0x[0-9a-fA-F]+\b|\b\d+(\.\d+)?[fFdDmM]?\b";
        // 10. 函数调用
        var functionPattern = @"\b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\()";
        // 11. Shell 命令
        var shellCommandPattern = @"\b(dotnet|add|package|cmd|npx|uvx|node|npm|git|echo|cd|ls|mkdir|rm|cp|mv)\b";

        // 组合所有模式（按优先级）
        var combinedPattern = $"({commentPattern})|({stringPattern})|({attributePattern})|({controlKeywords})|({declarationKeywords})|({typeKeywords})|({literalKeywords})|({typePattern})|({functionPattern})|({numberPattern})|({shellCommandPattern})";

        var regex = new Regex(combinedPattern, RegexOptions.None);
        var matches = regex.Matches(line);

        int lastIndex = 0;
        foreach (Match match in matches)
        {
            // 添加匹配前的普通文本
            if (match.Index > lastIndex)
            {
                textBlock.Inlines.Add(new Run(line.Substring(lastIndex, match.Index - lastIndex))
                {
                    Foreground = colorScheme.Default
                });
            }

            var run = new Run(match.Value);

            // 根据匹配的组设置颜色
            if (!string.IsNullOrEmpty(match.Groups[1].Value)) // 注释
                run.Foreground = colorScheme.Comment;
            else if (!string.IsNullOrEmpty(match.Groups[2].Value)) // 字符串
                run.Foreground = colorScheme.String;
            else if (!string.IsNullOrEmpty(match.Groups[3].Value)) // 特性
                run.Foreground = colorScheme.Attribute;
            else if (!string.IsNullOrEmpty(match.Groups[4].Value)) // 控制流关键字
                run.Foreground = colorScheme.ControlKeyword;
            else if (!string.IsNullOrEmpty(match.Groups[5].Value)) // 声明关键字
                run.Foreground = colorScheme.DeclarationKeyword;
            else if (!string.IsNullOrEmpty(match.Groups[6].Value)) // 类型关键字
                run.Foreground = colorScheme.TypeKeyword;
            else if (!string.IsNullOrEmpty(match.Groups[7].Value)) // 字面量
                run.Foreground = colorScheme.Literal;
            else if (!string.IsNullOrEmpty(match.Groups[8].Value)) // 类型
                run.Foreground = colorScheme.Type;
            else if (!string.IsNullOrEmpty(match.Groups[9].Value)) // 函数
                run.Foreground = colorScheme.Function;
            else if (!string.IsNullOrEmpty(match.Groups[10].Value)) // 数字
                run.Foreground = colorScheme.Number;
            else if (!string.IsNullOrEmpty(match.Groups[11].Value)) // Shell 命令
                run.Foreground = colorScheme.ShellCommand;
            else // 默认颜色
                run.Foreground = colorScheme.Default;

            textBlock.Inlines.Add(run);
            lastIndex = match.Index + match.Length;
        }

        // 添加剩余的普通文本
        if (lastIndex < line.Length)
        {
            textBlock.Inlines.Add(new Run(line.Substring(lastIndex, line.Length - lastIndex))
            {
                Foreground = colorScheme.Default
            });
        }
    }

    /// <summary>
    /// 配色方案接口
    /// </summary>
    private interface IColorScheme
    {
        Brush Default { get; }
        Brush Comment { get; }
        Brush String { get; }
        Brush Attribute { get; }
        Brush ControlKeyword { get; }
        Brush DeclarationKeyword { get; }
        Brush TypeKeyword { get; }
        Brush Literal { get; }
        Brush Type { get; }
        Brush Function { get; }
        Brush Number { get; }
        Brush ShellCommand { get; }
    }

    /// <summary>
    /// 深色主题配色方案（基于 VS Code Dark+ 主题）
    /// </summary>
    private class DarkColorScheme : IColorScheme
    {
        public Brush Default => new SolidColorBrush(Color.FromRgb(0xAB, 0xB2, 0xBF));
        public Brush Comment => new SolidColorBrush(Color.FromRgb(0x6A, 0x9A, 0x55));
        public Brush String => new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
        public Brush Attribute => new SolidColorBrush(Color.FromRgb(0xDD, 0xC7, 0xA1));
        public Brush ControlKeyword => new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0));
        public Brush DeclarationKeyword => new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        public Brush TypeKeyword => new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
        public Brush Literal => new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        public Brush Type => new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0));
        public Brush Function => new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA));
        public Brush Number => new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8));
        public Brush ShellCommand => new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0));
    }

    /// <summary>
    /// 浅色主题配色方案（基于 VS Code Light+ 主题，加深颜色以提高可读性）
    /// </summary>
    private class LightColorScheme : IColorScheme
    {
        // 深色文本，适合浅色背景
        public Brush Default => new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));           // 深灰色
        public Brush Comment => new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00));           // 深绿色
        public Brush String => new SolidColorBrush(Color.FromRgb(0xA3, 0x15, 0x15));            // 深红色
        public Brush Attribute => new SolidColorBrush(Color.FromRgb(0x79, 0x5E, 0x26));         // 棕色
        public Brush ControlKeyword => new SolidColorBrush(Color.FromRgb(0xAF, 0x00, 0xDB));    // 深紫色
        public Brush DeclarationKeyword => new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)); // 深蓝色
        public Brush TypeKeyword => new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0x99));       // 深青色
        public Brush Literal => new SolidColorBrush(Color.FromRgb(0x09, 0x85, 0x58));           // 深蓝绿色
        public Brush Type => new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0x99));              // 深青色
        public Brush Function => new SolidColorBrush(Color.FromRgb(0x79, 0x5E, 0x26));          // 棕色
        public Brush Number => new SolidColorBrush(Color.FromRgb(0x09, 0x85, 0x58));            // 深蓝绿色
        public Brush ShellCommand => new SolidColorBrush(Color.FromRgb(0xAF, 0x00, 0xDB));      // 深紫色
    }
}
