# MarkdView Guide

本文档存放实现细节、架构信息与扩展说明。`README.md` 仅保留快速上手和常用配置。

## 主题同步机制

无论使用哪种模式，`ThemeManager.CurrentTheme` 始终反映当前实际使用的主题：
- 模式 1：`ThemeManager.ApplyTheme()` 更新全局主题，所有 `Theme="Auto"` 的控件自动跟随
- 模式 2：控件 `Theme` 属性改变，更新控件主题并同步到 `ThemeManager.CurrentTheme`

## 可用主题资源键

主题资源文件：
- 浅色主题：`MarkdView/Themes/MarkdView.Light.xaml`
- 深色主题：`MarkdView/Themes/MarkdView.Dark.xaml`

常用资源键：
- `Markdown.Foreground` / `Markdown.Background`
- `Markdown.Heading.H1.Foreground` / `Markdown.Heading.H1.Border`
- `Markdown.Quote.Background` / `Markdown.Quote.Border`
- `Markdown.CodeBlock.Background` / `Markdown.CodeBlock.Foreground`
- `Markdown.CodeBlock.Header.Background`
- `Markdown.CodeBlock.CopyButton.Background` / `Markdown.CodeBlock.CopyButton.Foreground`
- `Markdown.InlineCode.Background` / `Markdown.InlineCode.Foreground`
- `Markdown.Link.Foreground`

语法高亮资源键：
- `Markdown.Syntax.Default` / `Markdown.Syntax.Comment` / `Markdown.Syntax.String` / `Markdown.Syntax.Attribute`
- `Markdown.Syntax.ControlKeyword` / `Markdown.Syntax.DeclarationKeyword` / `Markdown.Syntax.TypeKeyword` / `Markdown.Syntax.Literal`
- `Markdown.Syntax.Type` / `Markdown.Syntax.Function` / `Markdown.Syntax.Number` / `Markdown.Syntax.ShellCommand`

### 颜色控制范围

`MarkdView.Dark.xaml` / `MarkdView.Light.xaml` 已覆盖绝大多数渲染颜色（正文、标题、引用、表格、代码块容器、复制按钮、语法高亮）。

当前仍有少量颜色不由主题资源键直接控制：
- 代码块标题栏左侧 Mac 风格三色圆点（固定装饰色）
- 部分异常显示颜色（如渲染错误、图片加载失败）使用固定红色
- 资源键缺失时回退到代码中的默认兜底色

## 项目结构

```text
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

## 性能与优化

流式渲染优化：
- 自适应防抖：文档越大防抖间隔越大
- 重入保护：防止渲染过程重复触发
- 跳帧保护：最小渲染间隔 300ms
- 低优先级异步渲染：`DispatcherPriority.Background`

列表场景优化：
- 禁用内部滚动条时自动适配内容高度
- 鼠标滚轮事件冒泡到外层滚动容器
- 列表模式支持立即渲染
