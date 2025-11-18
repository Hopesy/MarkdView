using System.Windows;
using Samples.ViewModels;

namespace Samples.Views;

/// <summary>
/// MainWindow - 演示 MVVM 模式的使用
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 设置 DataContext 为 ViewModel
        DataContext = new MainWindowViewModel();
    }

    /// <summary>
    /// 打开 ScrollViewer 测试窗口
    /// </summary>
    private void OpenScrollViewerTest_Click(object sender, RoutedEventArgs e)
    {
        var testWindow = new ScrollViewerTestWindow();
        testWindow.Show();
    }

    /// <summary>
    /// 打开 Markdown 列表渲染测试窗口
    /// </summary>
    private void OpenMarkdownListTest_Click(object sender, RoutedEventArgs e)
    {
        var testWindow = new MarkdownListTestWindow();
        testWindow.Show();
    }
}
