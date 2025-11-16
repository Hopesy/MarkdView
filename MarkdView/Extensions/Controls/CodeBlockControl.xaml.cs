using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MarkdView.Extensions.Controls;

/// <summary>
/// 代码块控件，支持语法高亮和复制功能
/// </summary>
public partial class CodeBlockControl : UserControl
{
    /// <summary>
    /// 代码文本依赖属性
    /// </summary>
    public static readonly DependencyProperty CodeTextProperty =
        DependencyProperty.Register(nameof(CodeText), typeof(string), typeof(CodeBlockControl),
            new PropertyMetadata(string.Empty, OnCodeTextChanged));

    /// <summary>
    /// 语言标识依赖属性
    /// </summary>
    public static readonly DependencyProperty ProgrammingLanguageProperty =
        DependencyProperty.Register(nameof(ProgrammingLanguage), typeof(string), typeof(CodeBlockControl),
            new PropertyMetadata("code", OnLanguageChanged));

    /// <summary>
    /// 是否启用语法高亮依赖属性
    /// </summary>
    public static readonly DependencyProperty EnableSyntaxHighlightingProperty =
        DependencyProperty.Register(nameof(EnableSyntaxHighlighting), typeof(bool), typeof(CodeBlockControl),
            new PropertyMetadata(true, OnCodeTextChanged));

    public CodeBlockControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 代码文本
    /// </summary>
    public string CodeText
    {
        get => (string)GetValue(CodeTextProperty);
        set => SetValue(CodeTextProperty, value);
    }

    /// <summary>
    /// 语言标识
    /// </summary>
    public string ProgrammingLanguage
    {
        get => (string)GetValue(ProgrammingLanguageProperty);
        set => SetValue(ProgrammingLanguageProperty, value);
    }

    /// <summary>
    /// 是否启用语法高亮
    /// </summary>
    public bool EnableSyntaxHighlighting
    {
        get => (bool)GetValue(EnableSyntaxHighlightingProperty);
        set => SetValue(EnableSyntaxHighlightingProperty, value);
    }

    private static void OnCodeTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CodeBlockControl control)
        {
            control.UpdateCodeDisplay();
        }
    }

    private static void OnLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CodeBlockControl control)
        {
            control.LanguageLabel.Text = e.NewValue?.ToString() ?? "code";
            control.UpdateCodeDisplay();
        }
    }

    /// <summary>
    /// 更新代码显示
    /// </summary>
    private void UpdateCodeDisplay()
    {
        if (string.IsNullOrEmpty(CodeText))
        {
            CodeTextBlock.Inlines.Clear();
            return;
        }

        if (EnableSyntaxHighlighting)
        {
            ApplySyntaxHighlighting();
        }
        else
        {
            // 纯文本显示
            CodeTextBlock.Text = CodeText;
        }
    }

    /// <summary>
    /// 应用语法高亮
    /// </summary>
    private void ApplySyntaxHighlighting()
    {
        CodeTextBlock.Inlines.Clear();

        // 获取配色方案
        var defaultColor = TryGetResource<SolidColorBrush>("Markdown.CodeBlock.Foreground")?.Color ?? Colors.White;
        var keywordColor = TryGetResource<SolidColorBrush>("Markdown.Syntax.Keyword")?.Color ?? Colors.Magenta;
        var stringColor = TryGetResource<SolidColorBrush>("Markdown.Syntax.String")?.Color ?? Colors.Green;
        var numberColor = TryGetResource<SolidColorBrush>("Markdown.Syntax.Number")?.Color ?? Colors.Orange;
        var commentColor = TryGetResource<SolidColorBrush>("Markdown.Syntax.Comment")?.Color ?? Colors.Gray;

        var lines = CodeText.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // 简化的语法高亮逻辑
            var keywords = GetLanguageKeywords(ProgrammingLanguage);
            HighlightLine(line, keywords, keywordColor, stringColor, numberColor, commentColor, defaultColor);

            // 添加换行符（除了最后一行）
            if (i < lines.Length - 1)
            {
                CodeTextBlock.Inlines.Add(new LineBreak());
            }
        }
    }

    /// <summary>
    /// 高亮单行代码
    /// </summary>
    private void HighlightLine(string line, HashSet<string> keywords, Color keywordColor, Color stringColor,
        Color numberColor, Color commentColor, Color defaultColor)
    {
        // 检查是否是注释
        if (line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("#"))
        {
            CodeTextBlock.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush(commentColor) });
            return;
        }

        // 简单的词法分析
        var i = 0;
        while (i < line.Length)
        {
            // 跳过空白
            if (char.IsWhiteSpace(line[i]))
            {
                CodeTextBlock.Inlines.Add(new Run(line[i].ToString()));
                i++;
                continue;
            }

            // 字符串
            if (line[i] == '"' || line[i] == '\'' || line[i] == '`')
            {
                var quote = line[i];
                var start = i;
                i++;
                while (i < line.Length && line[i] != quote)
                {
                    if (line[i] == '\\' && i + 1 < line.Length) i++; // 跳过转义字符
                    i++;
                }
                if (i < line.Length) i++; // 包含结束引号

                var str = line.Substring(start, i - start);
                CodeTextBlock.Inlines.Add(new Run(str) { Foreground = new SolidColorBrush(stringColor) });
                continue;
            }

            // 数字
            if (char.IsDigit(line[i]))
            {
                var start = i;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == 'x' ||
                       char.IsLetter(line[i]))) // 支持十六进制和浮点数
                    i++;

                var num = line.Substring(start, i - start);
                CodeTextBlock.Inlines.Add(new Run(num) { Foreground = new SolidColorBrush(numberColor) });
                continue;
            }

            // 标识符或关键字
            if (char.IsLetter(line[i]) || line[i] == '_')
            {
                var start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                    i++;

                var word = line.Substring(start, i - start);
                var color = keywords.Contains(word) ? keywordColor : defaultColor;
                CodeTextBlock.Inlines.Add(new Run(word) { Foreground = new SolidColorBrush(color) });
                continue;
            }

            // 其他字符
            CodeTextBlock.Inlines.Add(new Run(line[i].ToString()) { Foreground = new SolidColorBrush(defaultColor) });
            i++;
        }
    }

    /// <summary>
    /// 获取语言关键字集合
    /// </summary>
    private HashSet<string> GetLanguageKeywords(string language)
    {
        return language.ToLower() switch
        {
            "csharp" or "cs" or "c#" => new HashSet<string>
            {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
                "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
                "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
                "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
                "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
                "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
                "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
                "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
                "virtual", "void", "volatile", "while", "async", "await", "var", "dynamic", "record"
            },
            "javascript" or "js" => new HashSet<string>
            {
                "abstract", "arguments", "await", "boolean", "break", "byte", "case", "catch", "char",
                "class", "const", "continue", "debugger", "default", "delete", "do", "double", "else",
                "enum", "eval", "export", "extends", "false", "final", "finally", "float", "for",
                "function", "goto", "if", "implements", "import", "in", "instanceof", "int", "interface",
                "let", "long", "native", "new", "null", "package", "private", "protected", "public",
                "return", "short", "static", "super", "switch", "synchronized", "this", "throw",
                "throws", "transient", "true", "try", "typeof", "var", "void", "volatile", "while",
                "with", "yield", "async"
            },
            "typescript" or "ts" => new HashSet<string>
            {
                "abstract", "any", "as", "async", "await", "boolean", "break", "case", "catch", "class",
                "const", "constructor", "continue", "debugger", "declare", "default", "delete", "do",
                "else", "enum", "export", "extends", "false", "finally", "for", "from", "function",
                "get", "if", "implements", "import", "in", "instanceof", "interface", "is", "keyof",
                "let", "module", "namespace", "never", "new", "null", "number", "of", "package",
                "private", "protected", "public", "readonly", "require", "return", "set", "static",
                "string", "super", "switch", "symbol", "this", "throw", "true", "try", "type", "typeof",
                "undefined", "var", "void", "while", "with", "yield"
            },
            "python" or "py" => new HashSet<string>
            {
                "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class",
                "continue", "def", "del", "elif", "else", "except", "finally", "for", "from", "global",
                "if", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise",
                "return", "try", "while", "with", "yield"
            },
            "java" => new HashSet<string>
            {
                "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class",
                "const", "continue", "default", "do", "double", "else", "enum", "extends", "final",
                "finally", "float", "for", "goto", "if", "implements", "import", "instanceof", "int",
                "interface", "long", "native", "new", "package", "private", "protected", "public",
                "return", "short", "static", "strictfp", "super", "switch", "synchronized", "this",
                "throw", "throws", "transient", "try", "void", "volatile", "while"
            },
            "go" or "golang" => new HashSet<string>
            {
                "break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough",
                "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range",
                "return", "select", "struct", "switch", "type", "var"
            },
            "rust" or "rs" => new HashSet<string>
            {
                "as", "async", "await", "break", "const", "continue", "crate", "dyn", "else", "enum",
                "extern", "false", "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod",
                "move", "mut", "pub", "ref", "return", "self", "Self", "static", "struct", "super",
                "trait", "true", "type", "unsafe", "use", "where", "while"
            },
            "swift" => new HashSet<string>
            {
                "associatedtype", "class", "deinit", "enum", "extension", "fileprivate", "func",
                "import", "init", "inout", "internal", "let", "open", "operator", "private", "protocol",
                "public", "rethrows", "static", "struct", "subscript", "typealias", "var", "break",
                "case", "continue", "default", "defer", "do", "else", "fallthrough", "for", "guard",
                "if", "in", "repeat", "return", "switch", "where", "while", "as", "catch", "false",
                "is", "nil", "super", "self", "Self", "throw", "throws", "true", "try"
            },
            _ => new HashSet<string>()
        };
    }

    /// <summary>
    /// 尝试获取资源
    /// </summary>
    private T? TryGetResource<T>(string key) where T : class
    {
        try
        {
            return Application.Current?.Resources[key] as T;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 复制按钮点击事件
    /// </summary>
    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 复制到剪贴板
            Clipboard.SetText(CodeText);

            // 显示成功反馈
            ShowCopySuccessAnimation();
        }
        catch
        {
            // 显示失败反馈
            ShowCopyFailureAnimation();
        }
    }

    /// <summary>
    /// 显示复制成功动画
    /// </summary>
    private void ShowCopySuccessAnimation()
    {
        // 保存原始文本
        var originalText = CopyText.Text;

        // 更改文本和颜色
        CopyText.Text = "已复制!";
        CopyIcon.Text = "✓";

        // 创建颜色动画
        var brush = new SolidColorBrush(Colors.Green);
        CopyText.Foreground = brush;
        CopyIcon.Foreground = brush;

        // 2秒后恢复
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        timer.Tick += (s, e) =>
        {
            CopyText.Text = originalText;
            CopyIcon.Text = "📋";
            CopyText.Foreground = TryGetResource<SolidColorBrush>("Markdown.CodeBlock.LabelForeground");
            CopyIcon.Foreground = TryGetResource<SolidColorBrush>("Markdown.CodeBlock.LabelForeground");
            timer.Stop();
        };
        timer.Start();
    }

    /// <summary>
    /// 显示复制失败动画
    /// </summary>
    private void ShowCopyFailureAnimation()
    {
        // 创建抖动动画
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 10,
            Duration = TimeSpan.FromMilliseconds(50),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3)
        };

        var transform = new TranslateTransform();
        CopyButton.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
