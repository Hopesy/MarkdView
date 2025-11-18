using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdView;
using MarkdView.Services.Theme;
using MarkdView.ViewModels;

namespace Samples;

/// <summary>
/// MainWindow 的 ViewModel - 演示 MVVM 模式
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.Dark;

    public MainWindowViewModel()
    {
        // 加载示例文件
        LoadMarkdownContent();
    }

    /// <summary>
    /// 切换到浅色主题
    /// </summary>
    [RelayCommand]
    private void SwitchToLightTheme()
    {
        Theme = ThemeMode.Light;
    }

    /// <summary>
    /// 切换到深色主题
    /// </summary>
    [RelayCommand]
    private void SwitchToDarkTheme()
    {
        Theme = ThemeMode.Dark;
    }

    /// <summary>
    /// 加载 Markdown 内容
    /// </summary>
    private void LoadMarkdownContent()
    {
        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Example.md");
            if (File.Exists(filePath))
            {
                Content = File.ReadAllText(filePath);
            }
            else
            {
                Content = "# 错误\n\n无法找到 Example.md 文件，请确保文件存在于应用程序目录中。";
            }
        }
        catch (Exception ex)
        {
            Content = $"# 错误\n\n加载 Example.md 文件时发生错误：\n\n```\n{ex.Message}\n```";
        }
    }
}
