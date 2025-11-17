using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace Samples
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? _markdownContent;

        public MainWindow()
        {
            InitializeComponent();
            LoadMarkdownContent();
        }

        private void LoadMarkdownContent()
        {
            try
            {
                string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Example.md");
                if (System.IO.File.Exists(filePath))
                {
                    _markdownContent = System.IO.File.ReadAllText(filePath);
                    MarkdownViewer.Markdown = _markdownContent;
                }
                else
                {
                    MarkdownViewer.Markdown = "# 错误\n\n无法找到 Example.md 文件，请确保文件存在于应用程序目录中。";
                }
            }
            catch (Exception ex)
            {
                MarkdownViewer.Markdown = $"# 错误\n\n加载 Example.md 文件时发生错误：\n\n```\n{ex.Message}\n```";
            }
        }

        private void LightThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyLightTheme();
        }

        private void DarkThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyDarkTheme();
        }

        private void HighContrastThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyHighContrastTheme();
        }

        private void ReloadBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadMarkdownContent();
        }

        private void ApplyLightTheme()
        {
            var resources = Application.Current.Resources;

            // 清除现有主题资源
            RemoveThemeResources();

            // 应用浅色主题
            resources["Markdown.Foreground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            resources["Markdown.Background"] = new SolidColorBrush(Colors.White);
            resources["Markdown.Heading.Border"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x9D, 0xFF));
            resources["Markdown.Quote.Background"] = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9));
            resources["Markdown.Quote.Border"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x9D, 0xFF));
            resources["Markdown.Table.Border"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            resources["Markdown.Table.HeaderBackground"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            resources["Markdown.CodeBlock.Background"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF8));
            resources["Markdown.Link.Foreground"] = new SolidColorBrush(Color.FromRgb(0x5C, 0x9D, 0xFF));
        }

        private void ApplyDarkTheme()
        {
            var resources = Application.Current.Resources;

            // 清除现有主题资源
            RemoveThemeResources();

            // 应用深色主题
            resources["Markdown.Foreground"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            resources["Markdown.Background"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            resources["Markdown.Heading.Border"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
            resources["Markdown.Quote.Background"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
            resources["Markdown.Quote.Border"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
            resources["Markdown.Table.Border"] = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x3E));
            resources["Markdown.Table.HeaderBackground"] = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
            resources["Markdown.CodeBlock.Background"] = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
            resources["Markdown.Link.Foreground"] = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
        }

        private void ApplyHighContrastTheme()
        {
            var resources = Application.Current.Resources;

            // 清除现有主题资源
            RemoveThemeResources();

            // 应用高对比度主题
            resources["Markdown.Foreground"] = new SolidColorBrush(Colors.White);
            resources["Markdown.Background"] = new SolidColorBrush(Colors.Black);
            resources["Markdown.Heading.Border"] = new SolidColorBrush(Colors.Yellow);
            resources["Markdown.Quote.Background"] = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x80));
            resources["Markdown.Quote.Border"] = new SolidColorBrush(Colors.Yellow);
            resources["Markdown.Table.Border"] = new SolidColorBrush(Colors.White);
            resources["Markdown.Table.HeaderBackground"] = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x80));
            resources["Markdown.CodeBlock.Background"] = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x80));
            resources["Markdown.Link.Foreground"] = new SolidColorBrush(Colors.Cyan);
        }

        private void RemoveThemeResources()
        {
            var resources = Application.Current.Resources;

            // 移除主题相关资源
            var keysToRemove = new[]
            {
                "Markdown.Foreground",
                "Markdown.Background",
                "Markdown.Heading.Border",
                "Markdown.Quote.Background",
                "Markdown.Quote.Border",
                "Markdown.Table.Border",
                "Markdown.Table.HeaderBackground",
                "Markdown.CodeBlock.Background",
                "Markdown.Link.Foreground"
            };

            foreach (var key in keysToRemove)
            {
                if (resources.Contains(key))
                {
                    resources.Remove(key);
                }
            }
        }
    }
}