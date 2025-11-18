using System;
using System.IO;
using System.Windows;
using Samples.Models;
using Samples.ViewModels;

namespace Samples.Views;

/// <summary>
/// Markdown 列表渲染测试窗口
/// </summary>
public partial class MarkdownListTestWindow : Window
{
    private readonly MarkdownListViewModel _viewModel;

    public MarkdownListTestWindow()
    {
        InitializeComponent();
        _viewModel = new MarkdownListViewModel();
        DataContext = _viewModel;

        // 初始加载一些示例数据
        LoadInitialData();
    }

    /// <summary>
    /// 加载 Example.md 内容
    /// </summary>
    private string LoadExampleMarkdown()
    {
        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Example.md");
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            else
            {
                return "# 错误\n\n无法找到 Example.md 文件。";
            }
        }
        catch (Exception ex)
        {
            return $"# 错误\n\n加载文件时发生错误：{ex.Message}";
        }
    }

    /// <summary>
    /// 加载初始数据
    /// </summary>
    private void LoadInitialData()
    {
        var exampleContent = LoadExampleMarkdown();

        _viewModel.AddItem(new MarkdownItem
        {
            Title = "Example.md - 卡片 1",
            Content = exampleContent
        });

        _viewModel.AddItem(new MarkdownItem
        {
            Title = "Example.md - 卡片 2",
            Content = exampleContent
        });

        _viewModel.AddItem(new MarkdownItem
        {
            Title = "Example.md - 卡片 3",
            Content = exampleContent
        });
    }

    /// <summary>
    /// 添加新消息
    /// </summary>
    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        var exampleContent = LoadExampleMarkdown();
        _viewModel.AddItem(new MarkdownItem
        {
            Title = $"Example.md - 卡片 #{_viewModel.Items.Count + 1}",
            Content = exampleContent
        });
    }

    /// <summary>
    /// 清空列表
    /// </summary>
    private void ClearItems_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "确定要清空所有消息吗？",
            "确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.ClearItems();
        }
    }

    /// <summary>
    /// 加载大量数据（性能测试）
    /// </summary>
    private void LoadManyItems_Click(object sender, RoutedEventArgs e)
    {
        var exampleContent = LoadExampleMarkdown();
        for (int i = 0; i < 20; i++)
        {
            _viewModel.AddItem(new MarkdownItem
            {
                Title = $"Example.md - 卡片 #{_viewModel.Items.Count + 1}",
                Content = exampleContent
            });
        }
    }
}
