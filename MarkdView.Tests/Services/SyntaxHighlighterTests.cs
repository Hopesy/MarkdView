using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MarkdView.Services;
using Xunit;

namespace MarkdView.Tests.Services;

public class SyntaxHighlighterTests
{
    [Fact]
    public void ApplyHighlighting_ShouldHighlightMultipleTokensInSingleLine()
    {
        var tokens = RunInSta(() =>
        {
            var textBlock = new TextBlock();
            SyntaxHighlighter.ApplyHighlighting(textBlock, "public class Demo { void Run() { var x = 1; } }", "csharp");
            var values = new List<(string Text, Color Color)>();
            foreach (var inline in textBlock.Inlines)
            {
                if (inline is Run run && run.Foreground is SolidColorBrush brush)
                    values.Add((run.Text ?? string.Empty, brush.Color));
            }
            return values;
        });

        Assert.Contains(tokens, token => token.Text == "class" && token.Color != Colors.Transparent);
        Assert.Contains(tokens, token => token.Text == "Demo");
        Assert.Contains(tokens, token => token.Text == "Run");
    }

    private static T RunInSta<T>(Func<T> func)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = func(); } catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw error;
        return result!;
    }
}
