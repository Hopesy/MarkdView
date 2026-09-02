# MarkdView

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download)
[![Version](https://img.shields.io/badge/Version-1.0.12-green.svg)](https://github.com/hopesy/MarkdView)

> 现代化 WPF Markdown 渲染控件，支持流式渲染、语法高亮和智能主题管理。

## ✨ 特性

- 🚀 **智能流式渲染** - 支持 AI 流式输出，自适应防抖优化（50ms-1000ms）
- 🎨 **语法高亮** - 内置多语言高亮支持
- 😊 **Emoji 支持** - 基于 Emoji.Wpf 的彩色 Emoji 渲染
- 💻 **Mac 风格代码块** - 带装饰性圆点的优雅代码展示
- 🌓 **统一主题管理** - 所有控件跟随 `ThemeManager` 的应用级主题
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

MarkdView 使用应用级主题资源字典，`ThemeManager.CurrentTheme` 是唯一真实来源。控件默认 `Theme="Auto"`，主题切换不会让某个控件覆盖其他控件。

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

        // 初始化全局主题（所有 MarkdownViewer 都会使用此主题）
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

#### `Theme` 属性说明

`Theme` 保留用于兼容旧 XAML。它只表达控件是否跟随全局主题，不会创建控件级资源字典；要切换颜色，请调用 `ThemeManager.ApplyTheme(ThemeMode.Light/Dark)`。

#### ThemeMode 枚举

```csharp
public enum ThemeMode
{
    Auto = 0,   // 跟随 ThemeManager（默认）
    Light = 1,  // 兼容值，不覆盖全局资源
    Dark = 2    // 兼容值，不覆盖全局资源
}
```

主题同步机制与设计细节见 [Guid.md](Guid.md)。

### 完整配置

```xaml
<markd:MarkdownViewer
    Content="{Binding Content}"
    Theme="Auto"
    EnableStreaming="True"
    StreamingThrottle="50"
    EnableSyntaxHighlighting="True"
    UseTransparentCanvas="False"
    ImageLoadTimeout="0:0:10"
    MaxImageBytes="8388608"
    MaxImagesPerDocument="64"
    MaxImageDecodePixel="1600"
    FontSize="12"
    FontFamily="Microsoft YaHei UI"
    VerticalScrollBarVisibility="Auto"
    HorizontalScrollBarVisibility="Auto" />
```

### 可测试的服务端口

默认构造路径适合普通 WPF 应用；需要统一网络策略、禁止打开外部链接或在测试中隔离系统副作用时，可以通过构造函数注入替换实现：

```csharp
using MarkdView.Interactions;
using MarkdView.Media;
using MarkdView.Services;

var viewer = new MarkdownViewer(
    imageLoader: new HttpMarkdownImageLoader(),
    linkHandler: new ShellMarkdownLinkHandler(),
    clipboardService: new WpfClipboardService(),
    syntaxHighlighter: new DefaultSyntaxHighlighter());
```

对应接口位于 `MarkdView.Media`、`MarkdView.Interactions` 和 `MarkdView.Services` 命名空间：

- `IMarkdownImageLoader`：图片下载、大小限制和解码策略；
- `IMarkdownLinkHandler`：外部链接打开策略；
- `IClipboardService`：代码复制端口；
- `ISyntaxHighlighter`：语法高亮策略；
- `IMarkdownParser`：Markdown 解析端口，可替换 Markdig 适配器。
- `IThemeService`：主题状态、资源切换和主题应用通知；默认实现为 `WpfThemeService`。

需要接入自定义解析器时，可直接实现 `IMarkdownDocumentParser` 并返回 `MarkdownDocumentModel`；模型构造函数会复制顶层块集合，避免解析器复用内部列表后改变已提交快照。

图片安全策略也可以直接在控件上配置：`ImageLoadTimeout`（大于 0 且不超过 10 分钟）、`MaxImageBytes`（1 到 256 MB）、`MaxImagesPerDocument`（0 到 4096，0 表示不加载图片）和 `MaxImageDecodePixel`（1 到 8192，限制解码后的最大宽/高）。这些属性会随每次渲染请求冻结，避免渲染过程中配置变化造成前后策略不一致。

这些接口的默认实现仍由无参构造提供，因此不会破坏现有 XAML。需要替换主题服务时，可以使用五参数构造函数传入 `IThemeService`；原有四参数构造函数继续保留。架构分层、迁移边界和后续文档模型计划见 [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)。

### 渲染配置快照

底层 renderer 支持使用 `MarkdownRenderOptions` 固定一次渲染所需的策略，避免渲染过程中读取变化中的限制：

```csharp
var options = new MarkdownRenderOptions(new FontFamily("Segoe UI"), 14)
{
    EnableSyntaxHighlighting = true,
    ImageLoadOptions = new MarkdownImageLoadOptions(
        timeout: TimeSpan.FromSeconds(10),
        maxBytes: 8 * 1024 * 1024)
    {
        MaxDecodePixel = 1600
    },
    MaxImagesPerDocument = 32
};
```

`WpfFlowDocumentRenderer` 会对旧版兼容 renderer 的属性做一次快照；未显式设置时继续使用 `MarkdownRenderer` 的默认限制。

### 字体与字号设置

`MarkdownViewer` 支持直接设置 `FontFamily` 和 `FontSize`，也支持数据绑定。

```xaml
<!-- 固定字体和字号 -->
<markd:MarkdownViewer
    Content="{Binding Content}"
    FontFamily="Microsoft YaHei UI"
    FontSize="14" />
```

```xaml
<!-- 绑定到 ViewModel -->
<markd:MarkdownViewer
    Content="{Binding Content}"
    FontFamily="{Binding MarkdownFontFamily}"
    FontSize="{Binding MarkdownFontSize}" />
```

说明：
- 修改 `FontFamily` / `FontSize` 后会立即重渲染并生效
- `FontSize` 会按比例影响正文、标题、列表和代码块

### 透明画布开关

`MarkdownViewer` 提供 `UseTransparentCanvas` 属性，用于控制渲染画布是否透明：

- `False`（默认）：使用主题资源 `Markdown.Background`，保证主题一致性和可读性
- `True`：将 `FlowDocument` 背景设为透明，适合嵌入已有卡片背景的场景

```xaml
<!-- 默认行为：使用主题背景 -->
<markd:MarkdownViewer Content="{Binding Content}" UseTransparentCanvas="False" />

<!-- 透明画布：继承父容器视觉背景 -->
<markd:MarkdownViewer Content="{Binding Content}" UseTransparentCanvas="True" />
```

### 语法高亮开关

`EnableSyntaxHighlighting` 用于控制代码块是否启用语法高亮：

- `True`（默认）：代码块按语法类型着色
- `False`：代码块以普通文本颜色显示

```xaml
<!-- 启用语法高亮（默认） -->
<markd:MarkdownViewer Content="{Binding Content}" EnableSyntaxHighlighting="True" />

<!-- 关闭语法高亮 -->
<markd:MarkdownViewer Content="{Binding Content}" EnableSyntaxHighlighting="False" />
```

### 语法高亮配色（动态资源）

代码块语法色使用动态资源键 `Markdown.Syntax.*`。主题切换时会自动刷新，不需要手动重新创建控件。

你可以在 `App.xaml` 或运行时覆盖这些键：

```xaml
<Application.Resources>
    <!-- 常用语法色覆盖示例 -->
    <SolidColorBrush x:Key="Markdown.Syntax.Default" Color="#1F2937" />
    <SolidColorBrush x:Key="Markdown.Syntax.Comment" Color="#6B7280" />
    <SolidColorBrush x:Key="Markdown.Syntax.String" Color="#B45309" />
    <SolidColorBrush x:Key="Markdown.Syntax.ControlKeyword" Color="#7C3AED" />
    <SolidColorBrush x:Key="Markdown.Syntax.Function" Color="#2563EB" />
</Application.Resources>
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

解析阶段在线程池执行，FlowDocument 创建和布局仍在 UI 线程。解析或构建失败时会触发 `RenderFailed`，随后仍触发 `RenderCompleted`，调用方可以统一结束 loading 状态。

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

可用主题资源键与颜色控制范围见 [Guid.md](Guid.md)。

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
| H1 标题 | 1.75× | 18px |
| H2 标题 | 1.42× | 17px |
| H3 标题 | 1.24× | 15px |
| H4 标题 | 1.12× | 13.5px |
| H5 标题 | 1.02× | 12px |
| H6 标题 | 0.96× | 11.5px |
| 正文 | 1.0× | 12px |
| 一级列表 | 1.08× | 13px |
| 嵌套列表 | 0.96× | 11.5px |
| 代码 | 0.92× | 11px |

```xaml
<!-- 全局调整字体大小 -->
<markd:MarkdownViewer FontSize="14" Content="{Binding Content}" />
```

项目结构、性能优化与实现细节见 [Guid.md](Guid.md)。

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
