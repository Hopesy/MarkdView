# MarkdView 项目结构

长期分层、依赖方向和迁移验收标准见 [ARCHITECTURE.md](ARCHITECTURE.md)。本文保留当前文件结构和运行职责说明。

## 当前目录

```text
MarkdView/
├── MarkdView/                 # NuGet 类库（net8.0-windows）
│   ├── Controls/              # MarkdownViewer 和渲染失败事件参数
│   ├── Renderers/             # Markdown AST、渲染选项、协调器和代码块渲染
│   │   ├── MarkdownRenderOptions.cs
│   │   ├── MarkdownRenderCoordinator.cs
│   │   └── MarkdownRenderSession.cs
│   ├── Parsing/               # Markdown 解析端口和 Markdig 适配器
│   ├── Documents/             # 稳定 Markdown 文档模型
│   ├── Interactions/          # 剪贴板和外部链接端口
│   ├── Media/                 # 图片加载端口、安全策略和 HTTP 实现
│   ├── Services/              # 语法高亮端口和默认实现
│   ├── Themes/                # Generic、Light、Dark 资源字典
│   ├── Enums/ThemeMode.cs
│   ├── ThemeManager.cs
│   └── AssemblyInfo.cs
├── MarkdView.Tests/            # xUnit + WPF STA 测试
├── Samples/                    # 可运行 WPF 示例
├── Docs/                       # 使用示例和项目说明
├── README.md
├── Guid.md
└── MarkdView.slnx
```

## 核心职责

### MarkdownViewer

`MarkdownViewer` 是 `ContentControl`，Markdown 文本通过 `Content` 属性提供。它负责模板承载、流式防抖、生命周期处理、主题事件订阅和渲染结果安装；解析取消、最新请求竞争和 Dispatcher 安装由 `MarkdownRenderCoordinator` 承担。

主要属性：

- `Content`: Markdown 字符串，空字符串也是有效值。
- `EnableStreaming`: 是否启用节流更新。
- `StreamingThrottle`: 1 到 10000 毫秒的节流间隔。
- `EnableSyntaxHighlighting`: 是否启用代码高亮。
- `Theme`: 兼容属性，默认 `Auto`；实际资源由全局 `ThemeManager` 控制。
- `FontFamily`、`FontSize`: 使用 WPF `Control` 属性的 owner，支持继承和有效值校验。
- `VerticalScrollBarVisibility`、`HorizontalScrollBarVisibility`。
- `UseTransparentCanvas`。
- `ImageLoadTimeout`、`MaxImageBytes`、`MaxImagesPerDocument`、`MaxImageDecodePixel`: 图片请求级安全限制，进入 `MarkdownRenderOptions` 快照。

渲染成功触发 `RenderCompleted`，失败触发 `RenderFailed`，并且两种结果都会触发 `RenderCompleted`，因此调用方不会永久卡在 loading 状态。

### MarkdownRenderer

渲染器通过 `IMarkdownParser` 获取 Markdig 0.43.0 AST。`ParseMarkdown` 只做纯解析，可以在线程池执行；`ConvertDocumentToFlowDocument` 在 UI 线程创建 WPF 对象。

`ParseDocumentModel` 返回不暴露 Markdig 类型的 `MarkdownDocumentModel`，包含内容哈希、块/内联语义、来源范围和嵌套关系，复杂扩展节点额外保留稳定类型名与 `RequiresCompatibilityRenderer` 标志，快照本身不持有 Markdig AST。解析端口和 WPF 输出端口已经拆开：`IMarkdownDocumentParser` 负责模型，`IMarkdownFlowDocumentRenderer` 负责 `FlowDocument`；`WpfFlowDocumentRenderer` 对已迁移块使用模型渲染，对未覆盖的顶层块按源片段执行兼容回退，后续迁移不会影响 coordinator 或控件。

当前覆盖标题、段落、引用、列表、任务列表、表格、代码块、分隔线、普通链接、自动链接、图片、HTML 文本降级、Emoji、删除线、粗体、斜体和行内代码。遇到未覆盖的节点会输出可读文本并记录 Debug 日志，不会静默丢失内容。

### MarkdownRenderCoordinator

协调器只接收文本快照和 `MarkdownRenderOptions`，负责在线程池解析、取消过期请求，并将 FlowDocument 构建封送到目标 Dispatcher。被替换或已取消的请求返回 `null`，控件不会安装过期结果。

外部链接仅允许 `http`、`https` 和 `mailto`。图片默认仅允许 `http`/`https`，限制 URI 长度、响应大小和数量，校验域名解析结果并禁止自动重定向；使用 `BitmapCacheOption.OnLoad` 和解码宽/高上限。图片端口返回抽象的 `BitmapSource`，默认 HTTP 实现返回冻结的 `BitmapImage`。本地文件、UNC、`data:` 等协议会显示阻止提示。

### CodeBlockRenderer

代码块是带语言标题、复制按钮和水平滚动的 `BlockUIContainer`；内容区域同时使用主题提供的最大高度，长代码在块内滚动而不会撑开整篇文档。语法高亮由 `SyntaxHighlighter` 完成，颜色使用 `Markdown.Syntax.*` 动态资源，随全局主题切换。代码块和常用块级间距由主题资源控制，资源缺失时由 `MarkdownLayoutDefaults` 提供一致的紧凑回退值。

### ThemeManager

主题是应用级资源字典，不支持控件之间互相独立覆盖。调用 `ThemeManager.ApplyTheme(ThemeMode.Light)` 或 `ApplyTheme(ThemeMode.Dark)` 完成全局切换；控件默认跟随当前主题。主题资源先加载成功再替换旧字典，失败时保留旧资源和旧状态。

`Themes/Generic.xaml` 使用程序集绝对 pack URI 引用控件模板，确保只引用 NuGet 包的外部 WPF 应用也能发现默认样式。`AssemblyInfo.cs` 中的 `ThemeInfo` 位于 MarkdView 程序集，而不是 Samples 程序集。

## 构建和验证

```powershell
dotnet restore MarkdView.slnx
dotnet build MarkdView.slnx --configuration Release --no-restore
dotnet test MarkdView.slnx --configuration Release --no-restore
dotnet pack MarkdView/MarkdView.csproj --configuration Release --no-restore
```

测试项目包含链接安全、图片协议、表格、删除线、自动链接、任务列表、语法高亮、主题字典和公共属性校验。WPF 对象测试必须在 STA 线程执行。
