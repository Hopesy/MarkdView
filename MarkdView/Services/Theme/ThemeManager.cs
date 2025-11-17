using System;
using System.Windows;

namespace MarkdView.Services.Theme;

/// <summary>
/// 主题管理器 - 简化主题切换操作
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// 主题类型枚举
    /// </summary>
    public enum Theme
    {
        /// <summary>浅色主题</summary>
        Light,

        /// <summary>深色主题</summary>
        Dark,

        /// <summary>高对比度主题</summary>
        HighContrast
    }

    /// <summary>
    /// 当前主题
    /// </summary>
    public static Theme CurrentTheme { get; private set; } = Theme.Light;

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public static event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// 应用主题到指定资源字典
    /// </summary>
    /// <param name="resourceDictionary">目标资源字典</param>
    /// <param name="theme">要应用的主题</param>
    public static void ApplyTheme(ResourceDictionary resourceDictionary, Theme theme)
    {
        if (resourceDictionary == null)
            throw new ArgumentNullException(nameof(resourceDictionary));

        var themeUri = GetThemeUri(theme);
        var themeDict = new ResourceDictionary { Source = themeUri };

        // 移除现有主题字典
        RemoveExistingThemes(resourceDictionary);

        // 添加新主题
        resourceDictionary.MergedDictionaries.Add(themeDict);

        CurrentTheme = theme;
        ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(theme));
    }

    /// <summary>
    /// 应用主题到应用程序资源
    /// </summary>
    /// <param name="theme">要应用的主题</param>
    public static void ApplyTheme(Theme theme)
    {
        if (Application.Current == null)
            throw new InvalidOperationException("Application.Current 为 null，无法应用主题");

        ApplyTheme(Application.Current.Resources, theme);
    }

    /// <summary>
    /// 获取主题资源 URI
    /// </summary>
    private static Uri GetThemeUri(Theme theme)
    {
        var themeName = theme switch
        {
            Theme.Light => "Light",
            Theme.Dark => "Dark",
            Theme.HighContrast => "HighContrast",
            _ => throw new ArgumentOutOfRangeException(nameof(theme))
        };

        return new Uri($"pack://application:,,,/MarkdView;component/Themes/{themeName}.xaml");
    }

    /// <summary>
    /// 移除现有主题字典
    /// </summary>
    private static void RemoveExistingThemes(ResourceDictionary resourceDictionary)
    {
        // 查找并移除 MarkdView 主题字典
        var toRemove = new System.Collections.Generic.List<ResourceDictionary>();

        foreach (var dict in resourceDictionary.MergedDictionaries)
        {
            if (dict.Source != null && dict.Source.ToString().Contains("MarkdView;component/Themes/"))
            {
                toRemove.Add(dict);
            }
        }

        foreach (var dict in toRemove)
        {
            resourceDictionary.MergedDictionaries.Remove(dict);
        }
    }

    /// <summary>
    /// 切换到下一个主题（循环切换）
    /// </summary>
    public static void ToggleTheme()
    {
        var nextTheme = CurrentTheme switch
        {
            Theme.Light => Theme.Dark,
            Theme.Dark => Theme.HighContrast,
            Theme.HighContrast => Theme.Light,
            _ => Theme.Light
        };

        ApplyTheme(nextTheme);
    }

    /// <summary>
    /// 主题变更事件参数
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public Theme NewTheme { get; }

        public ThemeChangedEventArgs(Theme newTheme)
        {
            NewTheme = newTheme;
        }
    }
}
