# MarkdView 可持续架构

本文档描述当前实现的真实边界、长期目标和迁移顺序。它不是把现有类名重新排列，而是规定职责、依赖方向和可验证的运行契约。

## 结论

当前版本已经具备可用的 WPF Markdown 控件，并已建立可持续演进所需的主要边界：解析、稳定文档模型、渲染协调、WPF 输出和外部副作用均有独立端口。仍有一项明确的技术债：兼容 `MarkdownRenderer` 保留了部分旧版 WPF 转换逻辑，复杂扩展还需要逐节点迁移。

这类实现短期修复速度快，长期会产生三个问题：

1. 新增一种 Markdown 节点会牵动样式、WPF 元素、异步加载和测试。
2. 网络、剪贴板、外部链接等副作用难以在没有真实桌面的情况下可靠测试。
3. `MarkdownViewer` 的生命周期和异步状态机与渲染细节耦合，虚拟化、缓存和增量渲染无法独立演进。

## 当前结构

当前主要依赖关系如下：

```text
MarkdownViewer
  ├─ Dispatcher / 生命周期与宿主属性
  ├─ ThemeManager (Application.Current 资源字典)
  └─ MarkdownRenderCoordinator
       ├─ IMarkdownDocumentParser
       │    └─ MarkdownRenderer (兼容组合实现)
       └─ IMarkdownFlowDocumentRenderer
            └─ WpfFlowDocumentRenderer
                 ├─ WpfSimpleMarkdownRenderer (模型驱动块)
                 └─ MarkdownRenderer (未覆盖顶层块的兼容回退)

MarkdownRenderer (兼容组合实现)
  ├─ IMarkdownParser / MarkdigMarkdownParser
  ├─ FlowDocument / Paragraph / Table / InlineUIContainer
  ├─ CodeBlockRenderer
  │    ├─ ISyntaxHighlighter
  │    ├─ IClipboardService
  │    └─ DynamicResource
  ├─ WpfMarkdownImageLoader -> IMarkdownImageLoader
  └─ WpfMarkdownLinkNavigator -> IMarkdownLinkHandler
```

这张图说明了当前最重要的架构事实：`MarkdownRenderer` 并不是单纯的 renderer，而是解析器、WPF 适配器和副作用服务的组合。

## 结构性风险

### 1. 渲染器职责过宽

`MarkdView/Renderers/MarkdownRenderer.cs` 目前仍同时负责：

- 解析结果接收和 Markdig AST 到 WPF 节点的转换（输入解析已委托给 `IMarkdownParser`）；
- FlowDocument 和所有 WPF 文档节点的创建；
- 主题资源查询和默认样式；
- 图片数量、大小、协议、DNS 和私网地址检查，以及图片 WPF 节点创建；
- `WpfMarkdownImageLoader` 的下载结果安装和失败占位编排；
- `WpfMarkdownLinkNavigator` 的 Hyperlink 事件编排；

这违反单一职责原则，也使渲染器无法在纯测试中隔离网络和操作系统行为。

### 2. 控件状态机和渲染实现耦合

`MarkdView/Controls/MarkdownViewer.xaml.cs` 同时管理依赖属性、模板重建、绑定延迟、主题订阅、父级滚动转发、流式防抖、版本竞争和错误文档。任何一个状态变化都可能触发完整 Markdown 重建。

当前已经把异步入口改为 `RenderMarkdownAsync`，并将解析取消、最新请求竞争和 Dispatcher 安装边界提取到 `MarkdownRenderCoordinator`；防抖和渲染竞争状态也已由 coordinator 统一管理，控件只负责 WPF 生命周期、最新文本、配置快照和结果安装。主题通过 `IThemeService` 注入，四参数旧构造函数继续保留。

### 3. 全局静态主题服务缺少线程边界

`ThemeManager` 直接修改 `Application.Current.Resources`。WPF 资源和控件具有 Dispatcher 线程亲和性，因此 `ApplyTheme` 必须明确要求 UI 线程，或者把切换请求封送到应用 Dispatcher。当前实现已经把后台调用封送到 `Application.Dispatcher`，并通过 `IThemeService/WpfThemeService` 为控件提供可替换端口；静态 `ThemeManager` 仅保留为兼容外观。

### 4. 副作用没有端口接口

剪贴板、外部链接和图片 HTTP 都是环境依赖，但现在由渲染器直接调用 `Clipboard.SetText`、`Process.Start` 和静态 `HttpClient`。这会导致：

- 单元测试只能检查生成的 WPF 对象，不能验证副作用是否正确触发；
- 宿主无法统一添加代理、审计、权限或禁用策略；
- 取消和窗口卸载的边界只能通过隐式字段配合。

### 5. 可扩展性和性能边界未形成契约

当前公共接口直接暴露 `FlowDocument`，没有中间渲染模型，也没有文档快照、节点统计、诊断信息或性能基线。因此增量渲染、虚拟化、服务端预渲染和不同 UI 后端都需要重新穿透现有实现。

## 目标分层

长期目标是单向依赖，UI 层不能反向驱动解析和基础服务：

```text
WPF Controls
  └─ RenderCoordinator
       ├─ MarkdownParser              (纯解析，Markdig 适配)
       ├─ MarkdownDocumentModel       (稳定的内部文档模型)
       ├─ IMarkdownFlowDocumentRenderer (模型 -> FlowDocument)
       │    └─ WpfFlowDocumentRenderer (唯一 WPF 适配器)
       ├─ IMarkdownImageLoader         (图片下载/解码端口)
       ├─ IMarkdownLinkHandler         (链接策略端口)
       ├─ IClipboardService            (剪贴板端口)
       └─ IThemeService                (主题状态和资源端口)
```

### Parsing 层

职责只有 Markdown 文本到 AST 或内部模型的转换，不创建任何 DependencyObject，不访问 `Application.Current`、Dispatcher、网络或剪贴板。

解析层应拥有：

- 可复用的 `MarkdownPipeline` 工厂（当前为 `MarkdownPipelineFactory.CreateDefault()`）；
- 输入文本和扩展配置；
- 可统计的节点类型和解析诊断；
- 独立的纯测试。

### Document Model 层

建议引入稳定的内部节点模型，例如 `MarkdownDocumentModel`、`MarkdownBlockModel` 和 `MarkdownInlineModel`。它不需要复制 Markdig 的全部类型，只保留 UI 和策略真正需要的数据：标题级别、文本、列表编号、链接 URI、代码语言、图片地址、来源位置和待处理状态。

这样可以让下游不依赖 Markdig 的具体 AST 版本，也可以在未来实现缓存、增量更新和非 WPF 输出。

### Rendering 层

渲染层只接收文档模型和 `MarkdownRenderOptions`。输出可以分成两步：

1. 生成无副作用的渲染描述或节点结果；
2. 由 WPF 适配器把结果安装到 FlowDocument。

`MarkdownRenderOptions` 已作为第一步落地，旧的多参数入口仍保留以避免破坏现有调用方。

### Side-effect Ports 层

推荐的最小端口：

```csharp
public interface IMarkdownImageLoader
{
    Task<BitmapSource> LoadAsync(Uri uri, MarkdownImageLoadOptions options, CancellationToken cancellationToken);
}

public interface IMarkdownLinkHandler
{
    void Open(Uri uri);
}

public interface IClipboardService
{
    void SetText(string text);
}
```

默认实现可以继续使用 HTTP、Shell 和 WPF Clipboard，但必须在组合根注册，而不是在 Markdown 节点转换方法中直接创建。测试使用内存 fake 即可验证调用、取消和失败行为。

### WPF Controls 层

`MarkdownViewer` 应只负责：

- 依赖属性和公开事件；
- 读取宿主提供的内容和配置；
- 监听 Loaded/Unloaded/模板重建；
- 将最新快照提交给 coordinator；
- 在 Dispatcher 上安装最新渲染结果。

主题、图片、链接、复制和节点转换不应继续扩展控件代码隐藏。

## 必须保持的契约

1. 同一个 `(content, options, themeVersion)` 只能安装一次；过期任务不能覆盖新文档。
2. 文档替换、控件卸载和 Dispatcher 关闭必须取消或忽略旧任务。
3. 解析失败、图片失败和链接策略拒绝必须分类报告，不能静默吞掉。
4. 渲染器默认不产生外部副作用；副作用由端口执行并可替换。
5. 主题切换是应用级事务：资源替换成功后更新版本并通知订阅者，失败保留旧状态。
6. `FlowDocument` 只能在 UI Dispatcher 创建和安装，Markdig 解析可以后台执行。
7. 所有尺寸、间距和颜色来自 `Markdown.*` 主题资源或明确的 `MarkdownRenderOptions`，禁止继续散落 magic number。主题资源缺失时由 `MarkdownLayoutDefaults` 提供统一回退值；模型 renderer 与兼容 renderer 不再各自维护一套默认间距。
8. 公共 API 保持向后兼容；新增能力优先增加 overload、options 或接口，不直接改变现有属性语义。

## 迁移顺序

### 阶段 0：已完成的稳定性基础

- 主题资源事务式替换；
- 内容和渲染版本竞争保护；
- 图片协议、地址、大小、超时和取消边界；
- `MarkdownRenderOptions` 配置快照（包含图片超时、响应大小、解码像素和文档图片数量策略）；
- `async void` 渲染入口收敛为 `RequestRender -> RenderMarkdownAsync`；
- 纯测试和 Windows 构建验证。

### 阶段 1：提取无副作用服务（已部分完成）

以下接口和默认实现已经落地，并且默认构造路径保持兼容：

1. `IMarkdownImageLoader` 和 `HttpMarkdownImageLoader`；
2. `IMarkdownLinkHandler` 和默认安全 Shell handler；
3. `IClipboardService` 和 WPF clipboard adapter；
4. `ISyntaxHighlighter`，把静态正则实现变成可替换策略。

`MarkdownRenderer` 和 `CodeBlockRenderer` 仍可创建 WPF 对象，但网络、Shell、Clipboard 和高亮策略均可由宿主注入。解析入口也已通过 `IMarkdownParser` 隔离，稳定模型不持有 Markdig AST；复杂扩展节点保留稳定类型名、源范围和 `RequiresCompatibilityRenderer` 标志，兼容回退仅依据模型源文本重新解析。图片超时、响应大小、解码像素和数量限制都在请求开始时冻结。

图片的异步下载结果安装已迁移到 `WpfMarkdownImageLoader`，Hyperlink 事件和外部链接策略已迁移到 `WpfMarkdownLinkNavigator`；`MarkdownRenderer` 只负责创建节点并连接端口。

### 阶段 2：拆分解析与 WPF 转换（已开始）

- 引入 `MarkdownDocumentModel`（内容哈希、块类型、来源范围和嵌套关系已落地；复杂扩展同时保留稳定类型名与显式兼容回退标志；快照不持有 Markdig AST）；
- `MarkdownDocumentModel` 已提供公开构造函数，允许自定义 `IMarkdownDocumentParser` 返回模型；构造时复制顶层块集合，避免外部解析器继续复用可变列表；
- 将 Markdig AST 到模型的转换独立出来；
- 块级模型已补充代码原文、标题、列表元数据和直接内联节点；内联模型覆盖文本、强调、删除线、代码、链接、图片、自动链接、换行、HTML 和任务状态；
- `IMarkdownDocumentParser` 和 `IMarkdownFlowDocumentRenderer` 已成为 coordinator 的分离依赖。`MarkdownRenderer` 仍实现组合兼容接口，控件实际使用 `WpfFlowDocumentRenderer` 作为 WPF 输出边界；当前适配器内部仍过渡委托给旧转换代码，后续迁移 block/inline 方法时不需要修改 coordinator 或控件；
- 图片加载和链接导航的 WPF 事件/异步处理已分别移至 `WpfMarkdownImageLoader` 与 `WpfMarkdownLinkNavigator`，网络和 Shell 副作用继续通过端口注入；
- 增加有界线程安全 `MarkdownDocumentCache`，按完整源文本复用不可变快照并采用 LRU 淘汰；同一源文本并发请求 single-flight，`Clear()` 通过 epoch 防止在途旧结果重新写回；
- 标题、普通段落、引用、列表、表格、水平分隔线以及文本/强调/删除线/行内代码/任务状态/普通链接/自动链接/图片/代码块/HTML/换行内联已由 `WpfSimpleMarkdownRenderer` 直接消费模型；未覆盖的扩展节点按顶层块回退到兼容 renderer，不会使整篇文档失去模型路径，兼容回退片段共享一次图片计数和取消会话，尚未宣称完成全部 WPF 适配器迁移；模型、适配器和缓存均有测试覆盖。引用和列表的嵌套关系、表格的列对齐与跨度、列表编号起点及主题间距均由模型数据驱动，链接导航仍统一经过协议白名单和注入的 `IMarkdownLinkHandler`，图片下载仍统一经过注入的 `IMarkdownImageLoader`，代码块仍通过可替换的 `CodeBlockRenderer` 保留高亮与剪贴板能力。

### 阶段 3：提取渲染 coordinator（核心链路已完成）

- 将取消令牌、防抖和最新请求竞争从 `MarkdownViewer` 移到 `MarkdownRenderCoordinator`；控件只计算内容长度对应的自适应延迟、创建配置快照并提交请求，协调器统一处理延迟取消、最新请求竞争、解析和 Dispatcher 安装；控件保留安装版本号作为 WPF 生命周期边界的最后一道检查；
- coordinator 以 `FlowDocument?` 区分成功与被替换/取消，控件将当前错误转换为 `RenderFailed` 并在成功或失败后触发 `RenderCompleted`；
- 控件只安装当前版本的 FlowDocument。

### 阶段 4：性能和产品能力

- 对大型文档增加解析缓存和内容哈希（已落地基础 LRU，仍需生产基线调参）；
- 在有真实基线后评估增量渲染或虚拟化；
- 增加节点数、解析耗时、WPF 构建耗时和图片等待时间诊断；
- 增加真实窗口回归和跨 DPI/主题的视觉快照。

## 验收标准

架构迁移不能只以“build 通过”为完成标准。每个阶段至少需要：

- `dotnet build ... -warnaserror` 通过；
- 纯模型/策略测试通过；
- WPF STA 适配测试通过；
- 图片、链接、剪贴板端口有 fake 实现测试；
- Samples 真实窗口启动、主题切换、滚动、复制和重挂载回归；
- 明确区分源码验证、离线规则测试和真实桌面验收。

## 当前阶段的边界

本次提交已经完成阶段 0，并部分完成阶段 1、阶段 2 和阶段 3：配置快照、异步入口、解析器端口、稳定文档模型、模型级内联语义、文档缓存、图片/链接/剪贴板/高亮端口、Dispatcher 线程边界以及渲染 coordinator 的取消/竞争控制已经落地。`MarkdownRenderer` 仍包含旧 WPF 转换逻辑，`WpfFlowDocumentRenderer` 通过顶层块混合回退维持兼容性；这仍不是最终的逐节点迁移，但已经避免整篇文档回退。后续新增功能应沿本文档的端口和分层推进，避免继续向 `MarkdownViewer.xaml.cs` 或 `MarkdownRenderer.cs` 添加横向职责。
