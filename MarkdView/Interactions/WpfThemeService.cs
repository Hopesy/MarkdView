using System;
using MarkdView.Enums;

namespace MarkdView.Interactions;

/// <summary>
/// 默认 WPF 主题适配器。静态 <see cref="global::MarkdView.ThemeManager"/> 仍作为兼容外观保留，
/// 新的控件和宿主代码应依赖 <see cref="IThemeService"/>。
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    public static WpfThemeService Default { get; } = new();

    public ThemeMode CurrentTheme => global::MarkdView.ThemeManager.CurrentTheme;

    public event EventHandler? ThemeApplied
    {
        add => global::MarkdView.ThemeManager.ThemeApplied += value;
        remove => global::MarkdView.ThemeManager.ThemeApplied -= value;
    }

    public void ApplyTheme(ThemeMode theme)
        => global::MarkdView.ThemeManager.ApplyTheme(theme);

    public void EnsureThemeApplied()
        => global::MarkdView.ThemeManager.EnsureThemeApplied();
}
