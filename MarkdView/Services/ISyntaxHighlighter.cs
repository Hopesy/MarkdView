using System.Windows.Controls;

namespace MarkdView.Services;

public interface ISyntaxHighlighter
{
    void ApplyHighlighting(TextBlock textBlock, string code, string? language);
}

public sealed class DefaultSyntaxHighlighter : ISyntaxHighlighter
{
    public void ApplyHighlighting(TextBlock textBlock, string code, string? language)
        => SyntaxHighlighter.ApplyHighlighting(textBlock, code, language);
}
