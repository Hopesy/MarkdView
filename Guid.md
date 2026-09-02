# MarkdView Guide

本文档记录当前实现的主题、渲染和扩展边界。快速上手请看 `README.md`。

## 主题模型

主题是应用级资源字典，由 `ThemeManager.ApplyTheme(ThemeMode.Light)` 或 `ApplyTheme(ThemeMode.Dark)` 切换。`ThemeManager.CurrentTheme` 只有在新资源字典成功加载并替换后才更新；加载失败会保留旧资源和旧状态。

`MarkdownViewer.Theme` 默认是 `Auto`，只保留为兼容属性，不创建控件级资源范围，也不会覆盖其他控件。需要改变显示主题时，应由应用统一调用 `ThemeManager`。

## 资源字典

- `MarkdView/Themes/Generic.xaml`: 默认控件模板入口。
- `MarkdView/Themes/MarkdView.Light.xaml`: 浅色主题。
- `MarkdView/Themes/MarkdView.Dark.xaml`: 深色主题。

常用资源键：

- `Markdown.Foreground` / `Markdown.Background`
- `Markdown.Toolbar.*`、`Markdown.Surface.*`、`Markdown.Divider`、`Markdown.Muted.Foreground`、`Markdown.Subtle.Foreground`、`Markdown.Accent`
- `Markdown.Heading.H1` 到 `Markdown.Heading.H6` 的 `Foreground` 和 `Border`
- `Markdown.Quote.Background` / `Markdown.Quote.Border` / `Markdown.Quote.Foreground`
- `Markdown.CodeBlock.*`、`Markdown.InlineCode.*`、`Markdown.Link.Foreground`
- `Markdown.Paragraph.Margin`、`Markdown.Heading.H1` 到 `H6` 的 `Margin`、`Markdown.Quote.Margin`、`Markdown.List.*`、`Markdown.Table.*`
- `Markdown.TaskList.Margin`、`Markdown.InlineCode.Padding`、`Markdown.Image.*`
- `Markdown.PagePadding`、`Markdown.LineHeight`、`Markdown.HorizontalRule.*`
- `Markdown.CodeBlock.Margin`、`Content.Padding`、`Content.MaxHeight`、`Header.Height`、`CodeFontScale`、`Dot.*`、`CopyButton.*`
- `Markdown.InlineCode.FontScale`、`LineHeightScale`、`MinHeightScale`
- `Markdown.Image.MaxWidth`、`TooltipMaxWidth`、`Placeholder.Margin`、`Margin`
- `Markdown.Syntax.Default`、`Comment`、`String`、`Attribute`、`ControlKeyword`、`DeclarationKeyword`、`TypeKeyword`、`Literal`、`Type`、`Function`、`Number`、`ShellCommand`

## 渲染边界

Markdown 文本通过 `MarkdownViewer.Content` 设置。Markdig 解析阶段可在线程池执行，FlowDocument 和其他 WPF 对象必须在 UI 线程创建。内容或配置在渲染期间变化时，旧结果会被版本号丢弃，最新请求会在当前渲染结束后继续处理。

支持的主要节点包括标题、段落、引用、普通/有序列表、任务列表、表格、代码块、分隔线、链接、自动链接、图片、HTML 文本降级、Emoji、删除线、粗体、斜体和行内代码。脚注、定义列表、数学公式等复杂扩展会在模型中保留稳定节点类型、源范围和兼容回退标志，当前通过兼容 renderer 输出可读内容；未支持节点不会静默丢失，并会记录 Debug 日志。

外部链接仅允许 `http`、`https`、`mailto`。图片仅允许 `http`/`https`，异步加载并默认使用 10 秒超时、8 MB 单图上限、64 张/文档上限和 1600 像素解码宽/高上限；控件可通过图片配置属性调整这些值，但仍受安全上限约束。请求不跟随重定向，并会校验域名解析结果。本地文件、UNC、`data:`、回环和私有 IP 地址会被拒绝。

解析、图片、链接、剪贴板和语法高亮均有独立端口，默认实现由 `MarkdownViewer` 的无参构造提供；宿主可以通过构造函数注入缓存、代理、应用内导航和测试 fake。图片端口返回抽象的 `BitmapSource`，而不是绑定具体解码类。控件的图片超时、单图字节数、解码像素和文档图片数量会在请求开始时冻结，保证同一文档内策略一致。

## 公共事件

- `RenderCompleted`: 成功或失败的渲染流程结束时都会触发。
- `RenderFailed`: 渲染失败时携带 `MarkdownRenderFailedEventArgs.Exception`。

事件处理器异常会单独记录，不会改变渲染结果或主题状态。

## 项目验证

```powershell
dotnet restore MarkdView.slnx
dotnet build MarkdView.slnx --configuration Release --no-restore -warnaserror
dotnet test MarkdView.slnx --configuration Release --no-restore
dotnet pack MarkdView/MarkdView.csproj --configuration Release --no-restore
```

`MarkdView.Tests` 使用 xUnit，涉及 WPF 控件的测试在 STA 线程运行。NuGet 包应包含 `README.md`、`LICENSE`、`Guid.md` 和 `icon.png`。
