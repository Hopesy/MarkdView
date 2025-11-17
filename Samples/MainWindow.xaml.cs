using System.Windows;

namespace Samples;

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
}
