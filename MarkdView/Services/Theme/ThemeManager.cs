using System;
using System.Linq;
using System.Windows;

namespace MarkdView.Services.Theme;

/// <summary>
/// 主题管理器 - 负责通过切换资源字典来管理全局主题
/// </summary>
public static class ThemeManager
{
    private const string LightThemeUri = "pack://application:,,,/MarkdView;component/Themes/Light.xaml";
    private const string DarkThemeUri = "pack://application:,,,/MarkdView;component/Themes/Dark.xaml";

    /// <summary>
    /// 主题应用完成事件 - 当主题资源字典被替换后触发
    /// </summary>
    public static event EventHandler? ThemeApplied;

    /// <summary>
    /// 获取当前应用的主题（通过检查已加载的资源字典）
    /// </summary>
    public static ThemeMode GetCurrentTheme()
    {
        var existingTheme = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null &&
                (d.Source.ToString().Contains("Light.xaml") ||
                 d.Source.ToString().Contains("Dark.xaml")));

        if (existingTheme != null && existingTheme.Source.ToString().Contains("Light.xaml"))
        {
            return ThemeMode.Light;
        }

        return ThemeMode.Dark;
    }

    /// <summary>
    /// 应用指定主题
    /// </summary>
    /// <param name="theme">要应用的主题</param>
    public static void ApplyTheme(ThemeMode theme)
    {
        var themeUri = theme switch
        {
            ThemeMode.Light => LightThemeUri,
            ThemeMode.Dark => DarkThemeUri,
            _ => DarkThemeUri
        };

        ApplyThemeFromUri(themeUri);
    }

    /// <summary>
    /// 从 URI 加载主题资源字典
    /// </summary>
    private static void ApplyThemeFromUri(string themeUri)
    {
        try
        {
            var uri = new Uri(themeUri, UriKind.Absolute);
            var newTheme = new ResourceDictionary { Source = uri };

            // 移除旧的主题资源字典
            RemoveExistingTheme();

            // 添加新的主题资源字典
            Application.Current.Resources.MergedDictionaries.Add(newTheme);

            // 触发主题应用完成事件
            ThemeApplied?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // 如果加载失败，记录错误但不中断程序
            System.Diagnostics.Debug.WriteLine($"加载主题失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 移除现有的主题资源字典
    /// </summary>
    private static void RemoveExistingTheme()
    {
        var existingTheme = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null &&
                (d.Source.ToString().Contains("Light.xaml") ||
                 d.Source.ToString().Contains("Dark.xaml")));

        if (existingTheme != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
        }
    }
}
