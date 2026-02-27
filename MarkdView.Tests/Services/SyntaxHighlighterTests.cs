using System;
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
            const string code = "public class Demo { void Run() { var x = 1; } }";

            SyntaxHighlighter.ApplyHighlighting(textBlock, code, "csharp", isLightTheme: false);

            var tokenColors = new List<TokenColor>();
            foreach (Inline inline in textBlock.Inlines)
            {
                if (inline is Run run)
                {
                    tokenColors.Add(new TokenColor(run.Text, GetColor(run)));
                }
            }

            return tokenColors;
        });

        var classToken = Assert.Single(tokens, r => r.Text == "class");
        var typeToken = Assert.Single(tokens, r => r.Text == "Demo");
        var functionToken = Assert.Single(tokens, r => r.Text == "Run");
        var defaultColor = Color.FromRgb(0xC9, 0xD1, 0xD9);
        Assert.Contains(tokens, r =>
            r.Color.A == defaultColor.A &&
            r.Color.R == defaultColor.R &&
            r.Color.G == defaultColor.G &&
            r.Color.B == defaultColor.B);

        var classColor = classToken.Color;
        var typeColor = typeToken.Color;
        var functionColor = functionToken.Color;

        Assert.NotEqual(defaultColor, classColor);
        Assert.NotEqual(defaultColor, typeColor);
        Assert.NotEqual(defaultColor, functionColor);
        Assert.NotEqual(classColor, typeColor);
        Assert.NotEqual(typeColor, functionColor);
    }

    private static Color GetColor(Run run)
    {
        var brush = Assert.IsType<SolidColorBrush>(run.Foreground);
        return brush.Color;
    }

    private readonly record struct TokenColor(string Text, Color Color);

    private static T RunInSta<T>(Func<T> func)
    {
        T? result = default;
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }

        return result!;
    }
}
