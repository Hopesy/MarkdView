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
        var commentPattern = @"(?<Comment>//.*$|/\*[\s\S]*?\*/|#.*$)";
        // 2. 字符串（第二优先级）
        var stringPattern = @"(?<String>(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|`[^`]*`))";
        // 3. C# 特性/装饰器
        var attributePattern = @"(?<Attribute>\[[\w\s,()=\[\]]+\])";
        // 4. 控制流关键字
        var controlKeywords = @"(?<Control>\b(?:if|else|switch|case|default|for|foreach|while|do|break|continue|return|throw|try|catch|finally|yield|await|async)\b)";
        // 5. 声明关键字
        var declarationKeywords = @"(?<Declaration>\b(?:class|interface|struct|enum|namespace|using|public|private|protected|internal|static|readonly|const|var|new|this|base|abstract|virtual|override|sealed|partial|delegate|event)\b)";
        // 6. 类型关键字
        var typeKeywords = @"(?<TypeKeyword>\b(?:void|int|long|short|byte|sbyte|uint|ulong|ushort|bool|char|string|float|double|decimal|object|dynamic)\b)";
        // 7. 字面量
        var literalKeywords = @"(?<Literal>\b(?:true|false|null)\b)";
        // 8. 泛型和类型名（类名/接口名等）
        var typePattern = @"(?<Type>\b[A-Z][a-zA-Z0-9]*(?:<[^>]+>)?(?=\s|<|,|;|:|\.|\[|\]))";
        // 9. 方法/函数调用
        var functionPattern = @"(?<Function>\b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\())";
        // 10. 数字
        var numberPattern = @"(?<Number>(\b0x[0-9a-fA-F]+\b|\b\d+(?:\.\d+)?[fFdDmM]?\b))";
        // 11. Shell 命令
        var shellCommandPattern = @"(?<Shell>\b(?:dotnet|add|package|cmd|npx|uvx|node|npm|git|echo|cd|ls|mkdir|rm|cp|mv)\b)";

        // 组合所有模式（按优先级）
        var combinedPattern = string.Join("|", new[]
        {
            commentPattern,
            stringPattern,
            attributePattern,
            controlKeywords,
            declarationKeywords,
            typeKeywords,
            literalKeywords,
            typePattern,
            functionPattern,
            numberPattern,
            shellCommandPattern
        });

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

            // 使用命名分组，避免嵌套捕获导致索引错位
            if (match.Groups["Comment"].Success)
                run.Foreground = colorScheme.Comment;
            else if (match.Groups["String"].Success)
                run.Foreground = colorScheme.String;
            else if (match.Groups["Attribute"].Success)
                run.Foreground = colorScheme.Attribute;
            else if (match.Groups["Control"].Success)
                run.Foreground = colorScheme.ControlKeyword;
            else if (match.Groups["Declaration"].Success)
                run.Foreground = colorScheme.DeclarationKeyword;
            else if (match.Groups["TypeKeyword"].Success)
                run.Foreground = colorScheme.TypeKeyword;
            else if (match.Groups["Literal"].Success)
                run.Foreground = colorScheme.Literal;
            else if (match.Groups["Type"].Success)
                run.Foreground = colorScheme.Type;
            else if (match.Groups["Function"].Success)
                run.Foreground = colorScheme.Function;
            else if (match.Groups["Number"].Success)
                run.Foreground = colorScheme.Number;
            else if (match.Groups["Shell"].Success)
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
    /// 深色主题配色方案（基于 GitHub Dark 主题）
    /// </summary>
    private class DarkColorScheme : IColorScheme
    {
        public Brush Default => new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9));             // 普通文本   #C9D1D9  RGB(201,209,217)  冷灰白
        public Brush Comment => new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E));             // 注释       #8B949E  RGB(139,148,158)  灰蓝色
        public Brush String => new SolidColorBrush(Color.FromRgb(0xA5, 0xD6, 0xFF));              // 字符串     #A5D6FF  RGB(165,214,255)  浅天蓝
        public Brush Attribute => new SolidColorBrush(Color.FromRgb(0xD2, 0xA8, 0xFF));           // 特性       #D2A8FF  RGB(210,168,255)  淡紫色
        public Brush ControlKeyword => new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x72));      // 控制关键字  #FF7B72  RGB(255,123,114)  珊瑚红
        public Brush DeclarationKeyword => new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x72));  // 声明关键字  #FF7B72  RGB(255,123,114)  珊瑚红
        public Brush TypeKeyword => new SolidColorBrush(Color.FromRgb(0x79, 0xC0, 0xFF));         // 内置类型    #79C0FF  RGB(121,192,255)  亮蓝色
        public Brush Literal => new SolidColorBrush(Color.FromRgb(0x79, 0xC0, 0xFF));             // 字面量      #79C0FF  RGB(121,192,255)  亮蓝色
        public Brush Type => new SolidColorBrush(Color.FromRgb(0xFF, 0xA6, 0x57));                // 类型名      #FFA657  RGB(255,166,87)   橙色
        public Brush Function => new SolidColorBrush(Color.FromRgb(0xD2, 0xA8, 0xFF));            // 方法名      #D2A8FF  RGB(210,168,255)  淡紫色
        public Brush Number => new SolidColorBrush(Color.FromRgb(0x79, 0xC0, 0xFF));              // 数字        #79C0FF  RGB(121,192,255)  亮蓝色
        public Brush ShellCommand => new SolidColorBrush(Color.FromRgb(0x7E, 0xE7, 0x87));        // Shell命令   #7EE787  RGB(126,231,135)  亮绿色
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
