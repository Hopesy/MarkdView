using System.Configuration;
using System.Data;
using System.Windows;
using MarkdView.Services.Theme;

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

            // 初始化默认主题为浅色
            ThemeManager.ApplyTheme(ThemeMode.Light);
        }
    }

}
