# MarkdView

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![Version](https://img.shields.io/badge/Version-1.0.10-green.svg)](https://github.com/hopesy/MarkdView)

> 现代化 WPF Markdown 渲染控件，支持流式渲染、语法高亮和智能主题管理。

## ✨ 特性

- 🚀 **智能流式渲染** - 支持 AI 流式输出，自适应防抖优化（50ms-1000ms）
- 🎨 **语法高亮** - 内置多语言高亮支持
- 😊 **Emoji 支持** - 基于 Emoji.Wpf 的彩色 Emoji 渲染
- 💻 **Mac 风格代码块** - 带装饰性圆点的优雅代码展示
- 🌓 **智能主题管理** - 支持自动跟随全局主题或独立设置
- 📐 **比例字体缩放** - 所有文本元素随 FontSize 成比例缩放
- 🔧 **易扩展** - 基于 Markdig，支持丰富的 Markdown 特性
- ⚡ **高性能** - 重入保护、低优先级异步渲染，确保 UI 流畅
- 📜 **列表场景优化** - 支持在 ScrollViewer 中禁用内部滚动条

## 📦 安装

```bash
# 使用 NuGet 包管理器
Install-Package MarkdView

# 或使用 .NET CLI
dotnet add package MarkdView
```

## 🚀 快速开始

### 基础用法

```xaml
<Window xmlns:markd="clr-namespace:MarkdView.Controls;assembly=MarkdView">
    <markd:MarkdownViewer Content="{Binding Content}" />
</Window>
```

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _content = "# Hello MarkdView\n\nThis is **bold** text.";
}
```

### 主题管理

MarkdView 提供智能主题管理系统，基于全局静态变量 `ThemeManager.CurrentTheme` 作为唯一真实来源，支持两种使用模式：

#### 模式 1：自动跟随全局主题（推荐）

不设置 `Theme` 属性（默认 `ThemeMode.Auto`），所有控件自动跟随全局主题：

```xaml
<!-- 所有控件自动跟随全局主题 -->
<markd:MarkdownViewer Content="{Binding Content}" />
<!-- 或显式设置为 Auto -->
<markd:MarkdownViewer Content="{Binding Content}" Theme="Auto" />
```

```csharp
using MarkdView;
using MarkdView.Enums;

// 在应用启动时初始化全局主题
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化全局主题（所有 Theme="Auto" 的控件都会使用此主题）
        ThemeManager.ApplyTheme(ThemeMode.Dark);
    }
}

// 运行时切换全局主题
ThemeManager.ApplyTheme(ThemeMode.Light);
ThemeManager.ApplyTheme(ThemeMode.Dark);

// 获取当前全局主题
var currentTheme = ThemeManager.CurrentTheme; // 始终返回当前实际使用的主题
```

**使用场景**：
- ✅ 应用中所有 Markdown 内容使用统一主题
- ✅ 主题由应用级别统一管理（如跟随系统主题）
- ✅ 简化主题管理逻辑

#### 模式 2：独立主题设置

显式设置 `Theme` 属性为 `Light` 或 `Dark`，控件使用独立主题（并同步到全局）：

```xaml
<!-- 控件使用独立主题（数据绑定） -->
<markd:MarkdownViewer
    Content="{Binding Content}"
    Theme="{Binding Theme}" />

<!-- 或直接设置固定主题 -->
<markd:MarkdownViewer
    Content="{Binding Content}"
    Theme="Dark" />
```

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ThemeMode _theme = ThemeMode.Dark;

    [RelayCommand]
    private void SwitchToLight()
    {
        // 修改此属性会：
        // 1. 更新控件主题
        // 2. 同步更新 ThemeManager.CurrentTheme
        Theme = ThemeMode.Light;
    }

    [RelayCommand]
    private void SwitchToDark()
    {
        Theme = ThemeMode.Dark;
    }
}
```

**使用场景**：
- ✅ 不同 Markdown 内容需要使用不同主题
- ✅ 主题切换由特定控件或 ViewModel 管理
- ✅ 需要通过数据绑定动态切换主题

#### ThemeMode 枚举

```csharp
public enum ThemeMode
{
    Auto = 0,   // 自动跟随全局主题（默认，推荐）
    Light = 1,  // 浅色主题
    Dark = 2    // 深色主题
}
```

#### 主题同步机制

无论使用哪种模式，`ThemeManager.CurrentTheme` 始终反映当前实际使用的主题：
- **模式 1**：`ThemeManager.ApplyTheme()` 更新全局主题 → 所有 `Theme="Auto"` 的控件自动跟随
- **模式 2**：控件 `Theme` 属性改变 → 更新控件主题并同步到 `ThemeManager.CurrentTheme`

这种设计确保了无论通过哪种方式改变主题，所有控件都能保持同步。

### 完整配置

```xaml
<markd:MarkdownViewer
    Content="{Binding Content}"
    Theme="Auto"
    EnableStreaming="True"
    StreamingThrottle="50"
    EnableSyntaxHighlighting="True"
    FontSize="12"
    FontFamily="Microsoft YaHei UI"
    VerticalScrollBarVisibility="Auto"
    HorizontalScrollBarVisibility="Auto" />
```

### 渲染完成事件

`MarkdownViewer` 提供 `RenderCompleted` 事件，用于监控 Markdown 内容何时完成渲染。这在需要显示加载动画、统计渲染性能或协调多个控件时非常有用。

#### 基础用法

```xaml
<markd:MarkdownViewer
    Content="{Binding Content}"
    RenderCompleted="OnMarkdownRenderCompleted" />
```

```csharp
private void OnMarkdownRenderCompleted(object sender, EventArgs e)
{
    // Markdown 渲染完成，可以隐藏 loading 动画
    LoadingIndicator.Visibility = Visibility.Collapsed;
}
```

#### 高级用法：通过 Attached Behavior 监控多个实例

在聊天应用等场景中，需要等待所有 AI 消息的 Markdown 都渲染完成后再显示内容：

**1. 创建 Attached Behavior：**

```csharp
public static class MarkdownLoadedBehavior
{
    private static int _loadedCount = 0;
    private static int _totalCount = 0;
    private static Action? _onAllLoaded;

    // 开始跟踪
    public static void StartTracking(string sessionId, int totalCount, Action onAllLoaded)
    {
        _loadedCount = 0;
        _totalCount = totalCount;
        _onAllLoaded = onAllLoaded;
    }

    // Attached Property
    public static readonly DependencyProperty IsTrackingEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsTrackingEnabled",
            typeof(bool),
            typeof(MarkdownLoadedBehavior),
            new PropertyMetadata(false, OnIsTrackingEnabledChanged));

    public static bool GetIsTrackingEnabled(DependencyObject obj)
        => (bool)obj.GetValue(IsTrackingEnabledProperty);

    public static void SetIsTrackingEnabled(DependencyObject obj, bool value)
        => obj.SetValue(IsTrackingEnabledProperty, value);

    private static void OnIsTrackingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer markdownViewer && (bool)e.NewValue)
        {
            markdownViewer.RenderCompleted += OnMarkdownRenderCompleted;
        }
    }

    private static void OnMarkdownRenderCompleted(object? sender, EventArgs e)
    {
        _loadedCount++;

        if (_loadedCount >= _totalCount && _totalCount > 0)
        {
            _onAllLoaded?.Invoke();  // 所有渲染完成
            _loadedCount = 0;
            _totalCount = 0;
            _onAllLoaded = null;
        }
    }
}
```

**2. 在 XAML 中使用：**

```xaml
<ItemsControl ItemsSource="{Binding Messages}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <markd:MarkdownViewer
                behaviors:MarkdownLoadedBehavior.IsTrackingEnabled="True"
                Content="{Binding Content}" />
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

**3. 在 ViewModel 中协调：**

```csharp
// 开始加载会话
IsLoadingMessages = true;

// 统计需要渲染的 Markdown 数量（例如：AI 消息数）
var markdownCount = session.Messages.Count(m => m.IsAIMessage);

// 开始跟踪渲染
MarkdownLoadedBehavior.StartTracking(
    sessionId,
    markdownCount,
    () =>
    {
        // 所有 Markdown 渲染完成后的回调
        Dispatcher.Invoke(() => IsLoadingMessages = false);
    }
);
```

#### 事件触发时机

`RenderCompleted` 事件在以下情况触发：
- Markdown 文本解析完成
- FlowDocument 构建完成
- 所有代码块语法高亮完成
- 文档配置和布局完成

**注意**：事件在 `DispatcherPriority.Background` 优先级下触发，确保 UI 主线程不被阻塞。

### 列表场景使用

在 `ScrollViewer` 中使用多个 `MarkdownViewer`（如聊天消息列表），需要禁用内部滚动条以实现流畅的外层滚动体验：

```xaml
<ScrollViewer VerticalScrollBarVisibility="Auto">
    <ItemsControl ItemsSource="{Binding Messages}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Margin="10" Padding="15" Background="White">
                    <!-- 重要：必须禁用 MarkdownViewer 的滚动条 -->
                    <markd:MarkdownViewer
                        Content="{Binding Content}"
                        VerticalScrollBarVisibility="Disabled"
                        HorizontalScrollBarVisibility="Disabled" />
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</ScrollViewer>
```

#### 列表场景滚动行为说明

**⚠️ 必须设置的属性**：
- 必须将 `VerticalScrollBarVisibility="Disabled"` 设置在每个 `MarkdownViewer` 上
- 必须将 `HorizontalScrollBarVisibility="Disabled"` 设置在每个 `MarkdownViewer` 上

**🎯 滚动行为**：
1. **外层文档滚动**：鼠标滚轮事件会自动转发给外层 `ScrollViewer`，实现流畅的列表滚动
2. **代码块滚动**：
   - 鼠标滚轮始终控制外层文档滚动（不会被代码块拦截）
   - 代码块内容只能通过拖动滚动条来滚动
   - 这样设计避免了滚动冲突，提供更好的用户体验
3. **透明容器支持**：即使外层 `Border` 背景设置为透明，滚动功能依然正常工作

**💡 代码块操作提示**：
- 复制代码：点击代码块右上角的复制按钮
- 滚动代码：拖动代码块内的滚动条（不支持鼠标滚轮）
- 文本选择：由于 WPF TextBlock 限制，暂不支持直接选中代码文本

## 🎨 主题定制

### 方式 1：运行时自定义（推荐）

在应用启动时加载主题并自定义颜色：

```csharp
using MarkdView;
using MarkdView.Enums;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 应用主题
        ThemeManager.ApplyTheme(ThemeMode.Dark);

        // 自定义特定颜色
        Resources["Markdown.Heading.H1.Border"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0xB4));
        Resources["Markdown.CodeBlock.Background"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    }
}
```

### 方式 2：在 App.xaml 中覆盖

```xaml
<Application.Resources>
    <!-- 覆盖默认主题颜色 -->
    <SolidColorBrush x:Key="Markdown.Foreground" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="Markdown.Heading.H1.Border" Color="#5C9DFF"/>
    <SolidColorBrush x:Key="Markdown.Quote.Background" Color="#F9F9F9"/>
    <SolidColorBrush x:Key="Markdown.CodeBlock.Background" Color="#282C34"/>
</Application.Resources>
```

### 可用的主题资源键

所有可自定义的主题资源键请参考：
- 浅色主题：`MarkdView/Themes/MarkdView.Light.xaml`
- 深色主题：`MarkdView/Themes/MarkdView.Dark.xaml`

主要资源键包括：
- `Markdown.Foreground` / `Markdown.Background` - 全局前景/背景色
- `Markdown.Heading.H1.Foreground` / `Markdown.Heading.H1.Border` - 标题样式
- `Markdown.Quote.Background` / `Markdown.Quote.Border` - 引用块样式
- `Markdown.CodeBlock.Background` / `Markdown.CodeBlock.Foreground` - 代码块样式
- `Markdown.CodeBlock.Header.Background` - 代码块头部（Mac 风格装饰）
- `Markdown.CodeBlock.CopyButton.Background` / `Foreground` - 复制按钮样式
- `Markdown.InlineCode.Background` / `Markdown.InlineCode.Foreground` - 行内代码样式
- `Markdown.Link.Foreground` - 链接颜色

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

## 📐 字体缩放系统

所有文本元素基于 `FontSize` 属性成比例缩放：

| 元素 | 缩放比例 | 示例（FontSize=12） |
|------|---------|-------------------|
| H1 标题 | 1.5× | 18px |
| H2 标题 | 1.25× | 15px |
| H3 标题 | 1.17× | 14px |
| H4 标题 | 1.08× | 13px |
| H5/H6 标题 | 1.0× | 12px |
| 正文 | 1.0× | 12px |
| 一级列表 | 1.08× | 13px |
| 嵌套列表 | 0.96× | 11.5px |
| 代码 | 0.92× | 11px |

```xaml
<!-- 全局调整字体大小 -->
<markd:MarkdownViewer FontSize="14" Content="{Binding Content}" />
```

## 🏗️ 项目结构

```
MarkdView/
├── Controls/
│   └── MarkdownViewer.xaml(.cs)    # 主 Markdown 渲染控件
├── Renderers/
│   ├── MarkdownRenderer.cs         # Markdown 渲染器
│   └── CodeBlockRenderer.cs        # 代码块渲染器（Mac 风格）
├── Enums/
│   └── ThemeMode.cs                # 主题模式枚举
├── ThemeManager.cs                 # 静态主题管理器
└── Themes/
    ├── MarkdView.Light.xaml        # 浅色主题资源字典
    └── MarkdView.Dark.xaml         # 深色主题资源字典
```

## 📊 性能与优化

### 流式渲染优化
- **自适应防抖** - 根据文档大小动态调整防抖时间
  - 0-2KB: 50ms
  - 2KB-10KB: 50ms → 300ms
  - 10KB-50KB: 300ms → 600ms
  - 50KB+: 1000ms
- **重入保护** - 防止渲染过程中的重复触发
- **跳帧保护** - 最小渲染间隔 300ms，避免 UI 卡顿
- **低优先级异步渲染** - 使用 `DispatcherPriority.Background` 保持 UI 响应

### 列表场景优化
- **智能滚动** - 禁用内部滚动条时自动适配高度
- **事件冒泡** - 透明容器也能正确响应鼠标滚轮事件
- **立即渲染** - 列表场景跳过流式防抖，直接渲染

### 精细化排版
- 标题层级分明（H1: 1.5×, H2: 1.25×, H3: 1.17×...）
- 列表缩进合理（首级 20px，嵌套每级 +5px）
- 列表标记大小适中（一级 1.08×，嵌套 0.96×）
- 代码字体略小（0.92×）以提高密度

## 🛠️ 技术栈

- **.NET 8.0** - 现代化的 .NET 平台
- **WPF** - Windows Presentation Foundation
- **Markdig 0.43.0** - 高性能 Markdown 解析器
- **Emoji.Wpf 0.3.4** - 彩色 Emoji 支持
- **CommunityToolkit.Mvvm** - MVVM 工具包（示例项目）

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
