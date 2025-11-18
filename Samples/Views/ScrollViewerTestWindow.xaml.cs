using System.Windows;
using Samples.ViewModels;

namespace Samples.Views;

/// <summary>
/// ScrollViewer 测试窗口
/// </summary>
public partial class ScrollViewerTestWindow : Window
{
    public ScrollViewerTestWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    /// <summary>
    /// 显示单个 MarkdownViewer 场景
    /// </summary>
    private void ShowSingleViewer_Click(object sender, RoutedEventArgs e)
    {
        SingleViewerPanel.Visibility = Visibility.Visible;
        ListViewerPanel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 显示列表模式场景
    /// </summary>
    private void ShowListViewer_Click(object sender, RoutedEventArgs e)
    {
        SingleViewerPanel.Visibility = Visibility.Collapsed;
        ListViewerPanel.Visibility = Visibility.Visible;
    }
}
