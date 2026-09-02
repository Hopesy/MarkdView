# MarkdView 路线图

## 已完成的稳定性修复

- [x] 修复 Dark 主题 XAML 语法错误和 Generic.xaml 运行时资源 URI。
- [x] 主题资源采用事务式替换，加载失败不破坏旧主题。
- [x] 统一全局主题模型，控件不会互相覆盖主题。
- [x] 支持删除线、自动链接、HTML 文本降级和任务列表复选框。
- [x] 未知 Markdown 节点不再静默丢失。
- [x] 图片协议白名单、URI 长度限制、地址解析校验和失败占位。
- [x] 图片下载使用每请求超时取消、响应大小上限和文档数量限制，并禁止自动重定向。
- [x] 修复渲染期间内容/配置更新丢失、空内容清空和卸载重挂载状态。
- [x] 恢复 xUnit WPF 测试项目并加入解决方案。
- [x] NuGet 包包含 README、LICENSE、icon.png 和 Guid.md。
- [x] 解析器、高亮、图片、链接和剪贴板支持可替换端口，并保留默认实现。
- [x] 主题状态通过 `IThemeService` 端口注入，静态 `ThemeManager` 保留为兼容外观。
- [x] 图片限制和代码块尺寸进入 `MarkdownRenderOptions`/`Markdown.*` 资源，单次渲染配置保持一致。
- [x] 主题资源访问明确 Dispatcher 边界，失效 Dispatcher 不再导致等待死锁。
- [x] 卸载期间阻止异步渲染重新排队，图片加载取消不会覆盖新文档。
- [x] 建立不暴露 Markdig 类型的 `MarkdownDocumentModel` 快照和内容哈希。
- [x] 统一模型 renderer 与兼容 renderer 的布局回退值，并将常用块级间距压缩到主题资源。
- [x] 将图片解码像素上限纳入控件配置和渲染请求快照，避免大图解码占用不可控内存。

## 下一阶段

- [x] 将 `MarkdownDocumentModel` 扩展为块/内联语义（文本、强调、删除线、代码、链接、图片、自动链接、换行、HTML、任务状态和源范围已覆盖）；继续增加扩展节点时保持向后兼容。
- [ ] 将剩余复杂节点的 WPF FlowDocument 转换全部迁移到独立的 `WpfFlowDocumentRenderer`（当前未覆盖块已按顶层块混合回退）。
- [x] 标题、普通段落、引用、列表、表格、水平分隔线、行内代码、任务状态、普通链接、自动链接、图片、代码块、HTML 文本降级和基础内联语义已迁移到模型驱动的 `WpfSimpleMarkdownRenderer`；未覆盖的扩展节点保留能力回退。
- [x] 为 coordinator 引入分离的 `IMarkdownDocumentParser` 与 `IMarkdownFlowDocumentRenderer` 端口，允许独立 WPF 适配器替换当前兼容实现。
- [x] 引入有界线程安全 `MarkdownDocumentCache`，按源文本做 LRU 快照复用，并支持并发 single-flight 与 Clear epoch。
- [x] 将图片异步安装和链接导航事件从 `MarkdownRenderer` 提取到独立 WPF 适配器。
- [x] 将防抖、版本竞争和取消状态迁移到 `MarkdownRenderCoordinator`；控件仅计算自适应延迟并提交最新请求。
- [ ] 为大文档引入增量渲染或虚拟化，并增加性能基准。
- [ ] 增加真实窗口自动化回归：主题按钮、滚动、代码复制和控件重挂载（当前仅完成 Samples 进程启动验证）。
- [ ] 完成更多 Markdig 扩展节点的原生 WPF 渲染策略（脚注、定义列表、数学公式等；当前已有稳定模型和兼容回退）。
- [x] 为脚注、定义列表和数学节点建立稳定模型类型与兼容回退标志，后续只需替换 WPF 节点 renderer。
- [x] 配置 GitHub Actions，在 Windows 上执行 restore、build、test、pack 和包内容检查。
- [ ] 评估升级 Markdig 前后的兼容性和渲染差异。

## 暂不支持的语义

- `Theme="Light"` 或 `Theme="Dark"` 不创建控件级主题；它们保留为兼容值。要切换主题，请调用 `ThemeManager.ApplyTheme`。
- 不提供 `Markdown` 属性、`AppendMarkdown()` 或 `Clear()` 方法；请使用 `Content` 属性。
- 不提供 `LinkClicked` 事件；链接通过 WPF `Hyperlink.RequestNavigate` 处理，并受协议白名单保护。
- 当前没有 `HighContrast.xaml`、`CodeBlockControl` 或 Markdig.Wpf 依赖。
