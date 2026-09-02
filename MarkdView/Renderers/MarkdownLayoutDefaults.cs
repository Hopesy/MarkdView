using System.Windows;

namespace MarkdView.Renderers;

/// <summary>
/// Markdown WPF 布局资源缺失时使用的统一回退值。
/// 主题资源是运行时首选来源；这些值只负责保证宿主未加载主题字典时仍能得到一致布局。
/// </summary>
internal static class MarkdownLayoutDefaults
{
    public const double LineHeightScale = 1.55;
    public static readonly Thickness PagePadding = new(12, 6, 12, 8);
    public static readonly Thickness ParagraphMargin = new(0, 1, 0, 4);
    public static readonly Thickness QuoteMargin = new(0, 5, 0, 7);
    public static readonly Thickness QuotePadding = new(8, 6, 8, 6);
    public static readonly Thickness ListMargin = new(0, 1, 0, 4);
    public static readonly Thickness ListItemMargin = new(0, 0, 0, 2);
    public static readonly Thickness TableMargin = new(0, 4, 0, 6);
    public static readonly Thickness TableCellPadding = new(5, 2, 5, 2);
    public static readonly Thickness CodeBlockMargin = new(0, 4, 0, 6);
    public static readonly Thickness CodeContentPadding = new(8, 4, 8, 4);
    public const double CodeContentMaxHeight = 480;
    public static readonly Thickness HorizontalRuleMargin = new(0, 6, 0, 6);
    public static readonly Thickness HeadingBorderThickness = new(0, 0, 0, 1);
    public static readonly Thickness QuoteBorderThickness = new(3, 0, 0, 0);
    public static readonly Thickness HorizontalRuleBorderThickness = new(0, 1, 0, 0);
    public static readonly Thickness InlineCodePadding = new(4, 0, 4, 0);
    public static readonly Thickness TaskListMargin = new(0, 0, 5, 0);
    public static readonly Thickness TableBorderThickness = new(1);
    public static readonly Thickness TableCellBorderThickness = new(0, 0, 1, 1);
    public static readonly Thickness ImageBorderThickness = new(0);
    public static readonly Thickness ImagePadding = new(4);
    public static readonly Thickness ImageMargin = new(0, 3, 0, 3);
    public static readonly Thickness ImagePlaceholderMargin = new(0, 3, 0, 3);
    public const double ImageMaxWidth = 800;
    public const double ImageTooltipMaxWidth = 300;
    public const double InlineCodeFontScale = 0.88;
    public const double InlineCodeLineHeightScale = 1.18;
    public const double InlineCodeMinHeightScale = 1.5;
    public static readonly CornerRadius CodeBlockCornerRadius = new(6);
    public static readonly CornerRadius CodeBlockHeaderCornerRadius = new(8, 8, 0, 0);
    public static readonly CornerRadius CopyButtonCornerRadius = new(4);
    public static readonly CornerRadius InlineCodeCornerRadius = new(4);
    public static readonly CornerRadius ImageCornerRadius = new(4);

    public static Thickness HeadingMargin(int level)
        => level switch
        {
            1 => new Thickness(0, 10, 0, 6),
            2 => new Thickness(0, 8, 0, 5),
            3 => new Thickness(0, 6, 0, 4),
            _ => new Thickness(0, 5, 0, 3)
        };

    public static Thickness HeadingPadding(int level)
        => level == 1
            ? new Thickness(0, 0, 0, 4)
            : level is 2 or 3
                ? new Thickness(0, 0, 0, 3)
                : new Thickness(0);
}
