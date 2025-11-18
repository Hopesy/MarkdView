using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdView.Services.Theme;

namespace MarkdView.ViewModels;

/// <summary>
/// Markdown 渲染器的 ViewModel - 管理所有状态和业务逻辑
/// </summary>
public partial class MarkdownViewModel : ObservableObject
{

    #region 可观察属性

    /// <summary>
    /// Markdown 文本内容
    /// </summary>
    [ObservableProperty]
    private string _markdown = string.Empty;

    /// <summary>
    /// 当前主题
    /// </summary>
    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.Dark;

    /// <summary>
    /// 是否启用语法高亮
    /// </summary>
    [ObservableProperty]
    private bool _enableSyntaxHighlighting = true;

    /// <summary>
    /// 是否启用流式渲染
    /// </summary>
    [ObservableProperty]
    private bool _enableStreaming = true;

    /// <summary>
    /// 流式渲染防抖间隔（毫秒）
    /// </summary>
    [ObservableProperty]
    private int _streamingThrottle = 50;

    /// <summary>
    /// 字体
    /// </summary>
    [ObservableProperty]
    private FontFamily _fontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");

    /// <summary>
    /// 字体大小
    /// </summary>
    [ObservableProperty]
    private double _fontSize = 14.0;

    #endregion

    #region 事件

    /// <summary>
    /// Markdown 内容变更事件
    /// </summary>
    public event EventHandler? MarkdownChanged;

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event EventHandler? ThemeChanged;

    /// <summary>
    /// 渲染设置变更事件（字体、字号、语法高亮等）
    /// </summary>
    public event EventHandler? RenderingSettingsChanged;

    #endregion

    #region 构造函数

    public MarkdownViewModel()
    {
        // 应用默认主题
        ThemeManager.ApplyTheme(_theme);
    }

    #endregion

    #region 属性变更通知

    /// <summary>
    /// Markdown 属性变更时调用
    /// </summary>
    partial void OnMarkdownChanged(string value)
    {
        MarkdownChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Theme 属性变更时调用
    /// </summary>
    partial void OnThemeChanged(ThemeMode value)
    {
        ThemeManager.ApplyTheme(value);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// EnableSyntaxHighlighting 属性变更时调用
    /// </summary>
    partial void OnEnableSyntaxHighlightingChanged(bool value)
    {
        RenderingSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// FontFamily 属性变更时调用
    /// </summary>
    partial void OnFontFamilyChanged(FontFamily value)
    {
        RenderingSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// FontSize 属性变更时调用
    /// </summary>
    partial void OnFontSizeChanged(double value)
    {
        RenderingSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 命令

    /// <summary>
    /// 应用浅色主题
    /// </summary>
    [RelayCommand]
    private void ApplyLightTheme()
    {
        Theme = ThemeMode.Light;
    }

    /// <summary>
    /// 应用深色主题
    /// </summary>
    [RelayCommand]
    private void ApplyDarkTheme()
    {
        Theme = ThemeMode.Dark;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 加载 Markdown 内容
    /// </summary>
    public void LoadMarkdown(string markdown)
    {
        Markdown = markdown;
    }

    /// <summary>
    /// 切换主题
    /// </summary>
    public void SwitchTheme(ThemeMode theme)
    {
        Theme = theme;
    }

    #endregion
}
