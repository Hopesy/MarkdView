# MarkdView

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![Version](https://img.shields.io/badge/Version-1.0.1-green.svg)](https://github.com/MinoChat/MarkdView)

> 现代化 WPF Markdown 渲染控件，支持流式渲染和语法高亮

## ✨ 特性

- 🚀 **流式渲染** - 支持 AI 流式输出，50ms 防抖优化
- 🎨 **语法高亮** - 内置多语言高亮支持
- 😊 **Emoji 支持** - 基于 Emoji.Wpf 的彩色 Emoji 渲染
- 💻 **Mac 风格代码块** - 带装饰性圆点的优雅代码展示
- 🌓 **主题切换** - 浅色/深色主题，支持自定义
- 📦 **MVVM 架构** - 完整支持数据绑定
- ⚡ **高性能** - 优化的渲染性能
- 🔧 **易扩展** - 基于 Markdig，支持丰富的 Markdown 特性

## 📦 安装

```bash
# 使用 NuGet 包管理器
Install-Package MarkdView

# 或使用 .NET CLI
dotnet add package MarkdView
```

**从源码引用**（当前阶段）:
```xml
<ProjectReference Include="..\MarkdView\MarkdView.csproj" />
```

## 🚀 快速开始

### 基础用法

```xaml
<Window xmlns:markd="clr-namespace:MarkdView.Controls;assembly=MarkdView">
    <markd:MarkdownViewer Markdown="{Binding Content}" />
</Window>
```

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _content = "# Hello MarkdView\n\nThis is **bold** text.";
}
```

### 主题切换

```xaml
<markd:MarkdownViewer
    Markdown="{Binding Content}"
    Theme="{Binding Theme}" />
```

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.Dark;

    [RelayCommand]
    private void SwitchToLight() => Theme = ThemeMode.Light;

    [RelayCommand]
    private void SwitchToDark() => Theme = ThemeMode.Dark;
}
```

### 完整配置

```xaml
<markd:MarkdownViewer
    Markdown="{Binding Content}"
    Theme="{Binding Theme}"
    EnableStreaming="True"
    StreamingThrottle="50"
    EnableSyntaxHighlighting="True"
    FontSize="14"
    FontFamily="Microsoft YaHei UI" />
```

## 🎨 主题定制

可以在 `App.xaml` 中自定义主题颜色：

```xaml
<Application.Resources>
    <!-- 自定义文本颜色 -->
    <SolidColorBrush x:Key="Markdown.Foreground" Color="#1E1E1E"/>

    <!-- 自定义标题边框 -->
    <SolidColorBrush x:Key="Markdown.Heading.H1.Border" Color="#5C9DFF"/>

    <!-- 自定义引用块 -->
    <SolidColorBrush x:Key="Markdown.Quote.Background" Color="#F9F9F9"/>
    <SolidColorBrush x:Key="Markdown.Quote.Border" Color="#5C9DFF"/>

    <!-- 自定义代码块 -->
    <SolidColorBrush x:Key="Markdown.CodeBlock.Background" Color="#282C34"/>
</Application.Resources>
```

更多主题键请参考主题资源字典文件 `Themes/Light.xaml` 和 `Themes/Dark.xaml`。

## 📝 支持的 Markdown 特性

### 基础语法
- ✅ 标题 (H1-H6)
- ✅ **粗体** / *斜体* / ~~删除线~~
- ✅ 段落和换行
- ✅ 引用块
- ✅ 有序/无序列表
- ✅ 链接和图片
- ✅ 水平分隔线

### 高级特性
- ✅ 代码块（Mac 风格设计 + 语法高亮）
- ✅ `行内代码`
- ✅ 表格
- ✅ 任务列表
- ✅ Emoji 😊
- ✅ GFM 扩展

### 语法高亮支持
C#, JavaScript, TypeScript, Python, Java, C/C++, Go, Rust, SQL, Bash, HTML, CSS, JSON, XML 等

## 🏗️ 项目结构

```
MarkdView/
├── Controls/
│   ├── MarkdownViewer.xaml(.cs)    # 主 Markdown 渲染控件
│   └── CodeBlockControl.xaml(.cs)  # 代码块控件
├── Renderers/
│   ├── MarkdownRenderer.cs         # Markdown 渲染器
│   └── CodeBlockRenderer.cs        # 代码块渲染器
├── Services/
│   ├── Theme/
│   │   ├── ThemeManager.cs         # 主题管理器（静态）
│   │   ├── ThemeService.cs         # 主题服务
│   │   └── ThemeMode.cs            # 主题模式枚举
│   └── SyntaxHighlight/
│       └── SyntaxHighlighter.cs    # 语法高亮服务
├── Themes/
│   ├── Light.xaml                  # 浅色主题资源字典
│   └── Dark.xaml                   # 深色主题资源字典
└── ViewModels/
    └── MarkdownViewModel.cs        # Markdown ViewModel
```

## 📊 性能特点

- 流式更新使用 50ms 防抖优化
- 支持大文档渲染
- 优化的 Markdown 解析和渲染性能

## 🛠️ 技术栈

- **.NET 8.0** - 现代化的 .NET 平台
- **WPF** - Windows Presentation Foundation
- **Markdig** - 高性能 Markdown 解析器
- **Emoji.Wpf** - 彩色 Emoji 支持
- **CommunityToolkit.Mvvm** - MVVM 工具包

## 🤝 贡献

欢迎提交 Issue 和 PR！

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 🙏 致谢

- [Markdig](https://github.com/xoofx/markdig) - 强大的 Markdown 解析器
- [Emoji.Wpf](https://github.com/samhocevar/emoji.wpf) - WPF Emoji 彩色渲染
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM 工具包

---

**Made with ❤️ for WPF developers**
