using System;
using MarkdView.Enums;

namespace MarkdView.Interactions;

/// <summary>
/// 主题状态和应用入口。控件通过此端口订阅主题变化，避免直接依赖静态主题实现。
/// </summary>
public interface IThemeService
{
    ThemeMode CurrentTheme { get; }

    event EventHandler? ThemeApplied;

    void ApplyTheme(ThemeMode theme);

    void EnsureThemeApplied();
}
