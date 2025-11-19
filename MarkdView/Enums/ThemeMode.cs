namespace MarkdView.Enums;

/// <summary>
/// Markdown 渲染主题
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// 自动 - 跟随全局主题（ThemeManager.CurrentTheme）
    /// </summary>
    Auto = 0,

    /// <summary>
    /// 浅色主题
    /// </summary>
    Light = 1,

    /// <summary>
    /// 深色主题
    /// </summary>
    Dark = 2
}
