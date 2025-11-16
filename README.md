# MarkdView

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![Version](https://img.shields.io/badge/Version-1.0.0-green.svg)](https://github.com/MinoChat/MarkdView)

> 现代化 WPF Markdown 渲染库，专为 AI 对话场景设计

## ✨ 特性

- 🚀 **流式渲染** - 完美支持 AI 流式输出，50ms 防抖优化
- 🎨 **语法高亮** - 内置 8+ 语言高亮，VS Code Dark+ 配色
- 😊 **Emoji 彩色渲染** - 基于 Emoji.Wpf 的完整 Emoji 支持
- 💻 **Mac 风格代码块** - 装饰性圆点和一键复制功能
- 🌓 **主题系统** - 浅色/深色/高对比度三套主题,支持自定义
- 📦 **开箱即用** - 无需配置，默认优美样式
- ⚡ **高性能** - 缓存机制 + 差分更新
- 🔧 **易扩展** - 基于 Markdig，支持所有 Markdig 扩展

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
    <markd:MarkdownViewer Markdown="{Binding MarkdownText}" />
</Window>
```

### 流式渲染（AI 场景）

```csharp
// ViewModel
public partial class ChatViewModel : ObservableObject
{
    [ObservableProperty]
    private string _aiResponse = "";

    public async Task StreamResponseAsync()
    {
        await foreach (var chunk in aiClient.StreamAsync())
        {
            AiResponse += chunk; // 自动触发流式渲染
        }
    }
}
```

```xaml
<markd:MarkdownViewer
    Markdown="{Binding AiResponse}"
    EnableStreaming="True"
    StreamingThrottle="50" />
```

### 高级配置

```xaml
<markd:MarkdownViewer
    Markdown="{Binding Content}"
    EnableStreaming="True"
    StreamingThrottle="50"
    EnableSyntaxHighlighting="True"
    FontSize="14"
    FontFamily="Microsoft YaHei UI, Segoe UI" />
```

## 🎨 主题定制

在 `App.xaml` 或资源字典中定义主题颜色：

```xaml
<ResourceDictionary>
    <!-- 文本颜色 -->
    <SolidColorBrush x:Key="Markdown.Foreground" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="Markdown.Background" Color="Transparent"/>

    <!-- 标题样式 -->
    <SolidColorBrush x:Key="Markdown.Heading.Border" Color="#5C9DFF"/>

    <!-- 引用块 -->
    <SolidColorBrush x:Key="Markdown.Quote.Background" Color="#F9F9F9"/>
    <SolidColorBrush x:Key="Markdown.Quote.Border" Color="#5C9DFF"/>

    <!-- 表格 -->
    <SolidColorBrush x:Key="Markdown.Table.Border" Color="#E0E0E0"/>
    <SolidColorBrush x:Key="Markdown.Table.HeaderBackground" Color="#F5F5F5"/>
</ResourceDictionary>
```

## 📝 支持的 Markdown 特性

- ✅ 标题 (H1-H6, 带下划线)
- ✅ 段落 & 换行
- ✅ **粗体** & *斜体*
- ✅ `行内代码`
- ✅ 代码块（Mac 风格 + 语法高亮 + 复制按钮）
- ✅ 引用块（左侧彩色边框）
- ✅ 有序/无序列表
- ✅ 表格（带边框样式）
- ✅ 链接（可点击）
- ✅ 图片
- ✅ 水平分隔线
- ✅ Emoji 😊（彩色渲染）
- ✅ 任务列表
- ✅ GFM 扩展

### 代码高亮支持

- C# / JavaScript / TypeScript
- Python / Java / C/C++
- Go / Rust / Swift / Kotlin
- SQL / Bash / PowerShell
- HTML / CSS / JSON / XML

## 🏗️ 架构设计

```
MarkdView/
├── Controls/
│   └── MarkdownViewer.xaml(.cs)    # 主控件
├── Renderers/
│   └── Blocks/
│       └── CodeBlockRenderer.cs     # 代码块渲染器
├── Extensions/
│   ├── Controls/
│   │   └── CodeBlockControl.xaml    # 代码块控件(复制功能)
│   ├── Behaviors/                   # 自定义行为
│   └── Converters/                  # 值转换器
├── Themes/
│   ├── Light.xaml                   # 浅色主题
│   ├── Dark.xaml                    # 深色主题
│   └── HighContrast.xaml            # 高对比度主题
└── Samples/
    ├── Markdown/                    # Markdown 示例
    └── Themes/                      # 主题切换示例
```

## 🔄 对比其他库

| 特性 | MarkdView | Markdig.Wpf | MdXaml |
|------|-----------|-------------|--------|
| 流式渲染 | ✅ | ❌ | ❌ |
| 语法高亮 | ✅ | ❌ | ⚠️ |
| 现代样式 | ✅ | ❌ | ❌ |
| 维护状态 | 🆕 | ⏸️ | ⏸️ |
| 性能优化 | ✅ | ⚠️ | ⚠️ |

## 📊 性能

- **首次渲染**: 1KB Markdown ~5ms
- **流式更新**: 50ms 防抖，CPU < 5%
- **大文档**: 100KB Markdown ~200ms
- **内存占用**: ~2MB (小型文档)

## 🛠️ 开发路线图

### v1.0.0 (已发布)
- [x] 基础渲染与流式支持
- [x] 完整语法高亮（8+ 语言）
- [x] Mac 风格代码块（三色圆点 + 复制按钮）
- [x] Emoji 彩色渲染（基于 Emoji.Wpf）
- [x] 完整主题系统（浅色/深色/高对比度）

### v1.1.0 (计划)
- [ ] 链接导航事件
- [ ] 代码块行号显示
- [ ] 更多语法高亮语言

### v1.2.0 (计划)
- [ ] LaTeX 数学公式支持
- [ ] 虚拟化渲染（大文档优化）
- [ ] 导出为 HTML/PDF

## 🤝 贡献

欢迎提交 Issue 和 PR！

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 🙏 致谢

- [Markdig](https://github.com/xoofx/markdig) - 强大的 Markdown 解析器
- [Emoji.Wpf](https://github.com/samhocevar/emoji.wpf) - WPF Emoji 彩色渲染

---

**Made with ❤️ for WPF & AI developers**
