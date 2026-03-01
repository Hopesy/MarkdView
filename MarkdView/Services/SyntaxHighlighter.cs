using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MarkdView.Services;

/// <summary>
/// 代码语法高亮器
/// </summary>
public static class SyntaxHighlighter
{
    // 按优先级匹配：注释 > 字符串 > 特性 > 关键字 > 类型 > 数字 > 函数
    private const string SyntaxPattern =
        @"(?<Comment>//.*$|/\*[\s\S]*?\*/|#.*$)" +
        @"|(?<String>(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|`[^`]*`))" +
        @"|(?<Attribute>\[[\w\s,()=\[\]]+\])" +
        @"|(?<Control>\b(?:if|else|switch|case|default|for|foreach|while|do|break|continue|return|throw|try|catch|finally|yield|await|async)\b)" +
        @"|(?<Declaration>\b(?:class|interface|struct|enum|namespace|using|public|private|protected|internal|static|readonly|const|var|new|this|base|abstract|virtual|override|sealed|partial|delegate|event)\b)" +
        @"|(?<TypeKeyword>\b(?:void|int|long|short|byte|sbyte|uint|ulong|ushort|bool|char|string|float|double|decimal|object|dynamic)\b)" +
        @"|(?<Literal>\b(?:true|false|null)\b)" +
        @"|(?<Type>\b[A-Z][a-zA-Z0-9]*(?:<[^>]+>)?(?=\s|<|,|;|:|\.|\[|\]))" +
        @"|(?<Function>\b[a-zA-Z_][a-zA-Z0-9_]*(?=\s*\())" +
        @"|(?<Number>(\b0x[0-9a-fA-F]+\b|\b\d+(?:\.\d+)?[fFdDmM]?\b))" +
        @"|(?<Shell>\b(?:dotnet|add|package|cmd|npx|uvx|node|npm|git|echo|cd|ls|mkdir|rm|cp|mv)\b)";

    private static readonly Regex SyntaxRegex = new(SyntaxPattern, RegexOptions.Compiled);

    /// <summary>
    /// 对 TextBlock 应用语法高亮
    /// </summary>
    public static void ApplyHighlighting(TextBlock textBlock, string code, string? language)
    {
        _ = language;
        var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            ApplyLineHighlighting(textBlock, lines[i]);

            if (i < lines.Length - 1)
            {
                textBlock.Inlines.Add(new LineBreak());
            }
        }
    }

    /// <summary>
    /// 对单行应用语法高亮
    /// </summary>
    private static void ApplyLineHighlighting(TextBlock textBlock, string line)
    {
        var matches = SyntaxRegex.Matches(line);

        int lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                AddStyledRun(
                    textBlock,
                    line.Substring(lastIndex, match.Index - lastIndex),
                    "Markdown.Syntax.Default",
                    Color.FromRgb(0xC9, 0xD1, 0xD9));
            }

            var run = new Run(match.Value);

            if (match.Groups["Comment"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.Comment", Color.FromRgb(0x8B, 0x94, 0x9E));
            else if (match.Groups["String"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.String", Color.FromRgb(0xA5, 0xD6, 0xFF));
            else if (match.Groups["Attribute"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.Attribute", Color.FromRgb(0xD2, 0xA8, 0xFF));
            else if (match.Groups["Control"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.ControlKeyword", Color.FromRgb(0xFF, 0x7B, 0x72));
            else if (match.Groups["Declaration"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.DeclarationKeyword", Color.FromRgb(0xFF, 0x7B, 0x72));
            else if (match.Groups["TypeKeyword"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.TypeKeyword", Color.FromRgb(0x79, 0xC0, 0xFF));
            else if (match.Groups["Literal"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.Literal", Color.FromRgb(0x79, 0xC0, 0xFF));
            else if (match.Groups["Type"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.Type", Color.FromRgb(0xFF, 0xA6, 0x57));
            else if (match.Groups["Function"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.Function", Color.FromRgb(0xD2, 0xA8, 0xFF));
            else if (match.Groups["Number"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.Number", Color.FromRgb(0x79, 0xC0, 0xFF));
            else if (match.Groups["Shell"].Success)
                SetSyntaxBrush(run, "Markdown.Syntax.ShellCommand", Color.FromRgb(0x7E, 0xE7, 0x87));
            else
                SetSyntaxBrush(run, "Markdown.Syntax.Default", Color.FromRgb(0xC9, 0xD1, 0xD9));

            textBlock.Inlines.Add(run);
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < line.Length)
        {
            AddStyledRun(
                textBlock,
                line.Substring(lastIndex, line.Length - lastIndex),
                "Markdown.Syntax.Default",
                Color.FromRgb(0xC9, 0xD1, 0xD9));
        }
    }

    private static void AddStyledRun(TextBlock textBlock, string text, string resourceKey, Color fallbackColor)
    {
        var run = new Run(text);
        SetSyntaxBrush(run, resourceKey, fallbackColor);
        textBlock.Inlines.Add(run);
    }

    private static void SetSyntaxBrush(TextElement element, string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.TryFindResource(resourceKey) != null)
        {
            element.SetResourceReference(TextElement.ForegroundProperty, resourceKey);
            return;
        }

        element.Foreground = new SolidColorBrush(fallbackColor);
    }
}
