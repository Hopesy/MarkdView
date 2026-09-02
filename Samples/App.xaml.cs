using System.Configuration;
using System.Data;
using System.Windows;
using MarkdView;
using MarkdView.Enums;

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

            // 先加载默认主题，避免窗口首次渲染时缺少 Markdown 资源而使用白底回退色。
            ThemeManager.ApplyTheme(ThemeMode.Dark);
        }
    }

}
