# MarkdView 使用示例

本文档提供 MarkdView 在不同场景下的代码示例。

## 1. 基础用法

### XAML 绑定

```xaml
<Window xmlns:markd="clr-namespace:MarkdView.Controls;assembly=MarkdView">
    <markd:MarkdownViewer Markdown="{Binding MarkdownText}" />
</Window>
```

### ViewModel

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _markdownText = "# Hello World\n\nThis is **MarkdView**!";
}
```

## 2. 流式渲染（AI 对话场景）

### ViewModel 实现

```csharp
public partial class ChatViewModel : ObservableObject
{
    [ObservableProperty]
    private string _aiResponse = "";

    public async Task SendMessageAsync(string userMessage)
    {
        AiResponse = ""; // 清空之前的内容

        // 调用 AI API（流式）
        await foreach (var chunk in aiClient.StreamCompletionAsync(userMessage))
        {
            AiResponse += chunk; // 自动触发流式渲染
            // MarkdownViewer 会自动防抖（50ms），不会频繁重绘
        }
    }
}
```

### XAML 配置

```xaml
<markd:MarkdownViewer
    Markdown="{Binding AiResponse}"
    EnableStreaming="True"
    StreamingThrottle="50"
    EnableSyntaxHighlighting="True" />
```

## 3. 手动控制渲染

### 使用 Code-Behind

```csharp
public partial class MyWindow : Window
{
    public MyWindow()
    {
        InitializeComponent();

        // 方式 1: 设置完整内容
        MarkdownViewer.Markdown = "# Title\n\nContent...";

        // 方式 2: 追加内容（流式场景）
        MarkdownViewer.AppendMarkdown("\n\n新增的段落");

        // 方式 3: 清空内容
        MarkdownViewer.Clear();
    }
}
```

## 4. 禁用流式渲染（静态内容）

适用于显示固定的 Markdown 文档（如帮助文档、文章等）。

```xaml
<markd:MarkdownViewer
    Markdown="{Binding DocumentContent}"
    EnableStreaming="False" />
```

## 5. 调整防抖间隔

根据不同的 AI 输出速度调整：

```xaml
<!-- 快速 AI（如 GPT-4）：减少延迟 -->
<markd:MarkdownViewer
    StreamingThrottle="30" />

<!-- 慢速 AI 或网络不稳定：增加间隔 -->
<markd:MarkdownViewer
    StreamingThrottle="100" />
```

## 6. 禁用语法高亮（提升性能）

如果不需要代码高亮，可以禁用以提升渲染性能：

```xaml
<markd:MarkdownViewer
    EnableSyntaxHighlighting="False" />
```

## 7. 自定义字体和样式

```xaml
<markd:MarkdownViewer
    Markdown="{Binding Content}"
    FontFamily="Consolas, Courier New"
    FontSize="16"
    Foreground="White"
    Background="#1E1E1E" />
```

## 8. 主题定制

在 `App.xaml` 或资源字典中定义主题颜色：

```xaml
<Application.Resources>
    <!-- 文本颜色 -->
    <SolidColorBrush x:Key="Markdown.Foreground" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="Markdown.Background" Color="Transparent"/>

    <!-- 标题下划线 -->
    <SolidColorBrush x:Key="Markdown.Heading.Border" Color="#5C9DFF"/>

    <!-- 引用块 -->
    <SolidColorBrush x:Key="Markdown.Quote.Background" Color="#F9F9F9"/>
    <SolidColorBrush x:Key="Markdown.Quote.Border" Color="#5C9DFF"/>

    <!-- 表格 -->
    <SolidColorBrush x:Key="Markdown.Table.Border" Color="#E0E0E0"/>
    <SolidColorBrush x:Key="Markdown.Table.HeaderBackground" Color="#F5F5F5"/>
</Application.Resources>
```

## 9. 深色主题示例

```xaml
<Application.Resources>
    <SolidColorBrush x:Key="Markdown.Foreground" Color="#CCCCCC"/>
    <SolidColorBrush x:Key="Markdown.Background" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="Markdown.Heading.Border" Color="#569CD6"/>
    <SolidColorBrush x:Key="Markdown.Quote.Background" Color="#2D2D2D"/>
    <SolidColorBrush x:Key="Markdown.Quote.Border" Color="#569CD6"/>
    <SolidColorBrush x:Key="Markdown.Table.Border" Color="#3E3E3E"/>
    <SolidColorBrush x:Key="Markdown.Table.HeaderBackground" Color="#2D2D2D"/>
</Application.Resources>
```

## 10. 动态切换主题

```csharp
public void ApplyTheme(bool isDarkMode)
{
    var resources = Application.Current.Resources;

    if (isDarkMode)
    {
        resources["Markdown.Foreground"] = new SolidColorBrush(Colors.White);
        resources["Markdown.Background"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        // ... 其他颜色
    }
    else
    {
        resources["Markdown.Foreground"] = new SolidColorBrush(Colors.Black);
        resources["Markdown.Background"] = new SolidColorBrush(Colors.White);
        // ... 其他颜色
    }
}
```

## 11. 完整 AI 聊天示例

```csharp
public partial class ChatPageViewModel : ObservableObject
{
    private readonly IAiClient _aiClient;

    [ObservableProperty]
    private string _currentMessage = "";

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        // 添加用户消息
        var userMsg = new ChatMessage
        {
            IsUser = true,
            Content = CurrentMessage
        };
        Messages.Add(userMsg);

        // 创建 AI 消息占位符
        var aiMsg = new ChatMessage
        {
            IsUser = false,
            Content = ""
        };
        Messages.Add(aiMsg);

        // 流式接收 AI 响应
        await foreach (var chunk in _aiClient.StreamAsync(CurrentMessage))
        {
            aiMsg.Content += chunk; // MarkdownViewer 自动更新
        }

        CurrentMessage = ""; // 清空输入框
    }
}

public class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private bool _isUser;

    [ObservableProperty]
    private string _content = "";
}
```

### 对应的 XAML

```xaml
<ItemsControl ItemsSource="{Binding Messages}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Margin="10"
                    Padding="15"
                    Background="{Binding IsUser, Converter={StaticResource BoolToBackgroundConverter}}"
                    CornerRadius="8">

                <!-- 用户消息：纯文本 -->
                <TextBlock Text="{Binding Content}"
                           Visibility="{Binding IsUser, Converter={StaticResource BoolToVisibilityConverter}}"
                           TextWrapping="Wrap" />

                <!-- AI 消息：Markdown 渲染 -->
                <markd:MarkdownViewer
                    Markdown="{Binding Content}"
                    EnableStreaming="True"
                    Visibility="{Binding IsUser, Converter={StaticResource InverseBoolToVisibilityConverter}}" />
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

## 12. 性能优化建议

### 大文档渲染

```csharp
// 对于超大文档（>100KB），禁用流式渲染
MarkdownViewer.EnableStreaming = false;
MarkdownViewer.Markdown = largeDocument;
```

### 高频更新场景

```csharp
// 如果 AI 输出非常快，增加防抖间隔
MarkdownViewer.StreamingThrottle = 100; // 降低更新频率
```

### 内存管理

```csharp
// 切换会话时清空旧内容
MarkdownViewer.Clear();
await Task.Delay(10); // 给 GC 时间回收
MarkdownViewer.Markdown = newSessionContent;
```

## 支持的 Markdown 语法

参考 [README.md](README.md) 的"支持的 Markdown 特性"部分。

## 疑难解答

### 问题 1: 代码高亮不生效
**解决**: 确保设置了 `EnableSyntaxHighlighting="True"` 并且代码块指定了语言：
```markdown
\`\`\`csharp
// 你的代码
\`\`\`
```

### 问题 2: 流式渲染卡顿
**解决**: 增加 `StreamingThrottle` 值（例如 100-200ms）

### 问题 3: 主题颜色不生效
**解决**: 确保资源键已在 `Application.Resources` 中定义，并且在 MarkdownViewer 初始化之前加载

---

**更多示例请查看 `/Examples/BasicUsage.xaml` 文件**
