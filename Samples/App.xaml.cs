using System.Configuration;
using System.Data;
using System.Windows;

namespace Samples
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 不在这里初始化主题，让 MarkdownViewer 控件自己的 Theme 属性决定
            // ThemeManager.ApplyTheme(ThemeMode.Light);
        }
    }

}
