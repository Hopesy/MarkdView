# MarkdView 待办事项

## 🚀 v0.2.0 - 主题与交互增强

### 主题系统
- [x] 创建 `Themes/Light.xaml` - 完整的浅色主题资源字典
- [x] 创建 `Themes/Dark.xaml` - 完整的深色主题资源字典
- [x] 创建 `Themes/HighContrast.xaml` - 高对比度主题
- [x] 添加 `ThemeManager` 类用于动态切换主题
- [x] 主题预览示例（在 Samples/Themes/ 中）

### 代码块增强
- [x] 添加代码块复制按钮
  - [x] 创建 `CodeBlockControl` 自定义控件
  - [x] 复制到剪贴板功能
  - [x] 复制成功提示动画
  - [x] 鼠标悬停显示按钮
- [ ] 行号显示选项
- [ ] 代码折叠功能（长代码块）

### 语法高亮扩展
- [x] 支持更多语言：
  - [x] Rust
  - [x] Swift
  - [x] Go
  - [ ] Kotlin
  - [ ] PHP
  - [ ] Ruby
  - [ ] SQL
  - [ ] Bash/Shell
  - [ ] PowerShell
  - [ ] HTML/XML
  - [ ] CSS
- [ ] 考虑集成完整的语法高亮库（ColorCode.Core 或 AvalonEdit）
- [ ] 支持自定义语法高亮规则

### 交互功能
- [x] 链接点击事件处理
  - [x] `LinkClicked` 事件
  - [x] 自定义链接处理器
  - [ ] 默认浏览器打开（需在外部实现）
- [ ] 图片功能增强
  - [ ] 加载状态指示器
  - [ ] 加载失败占位符
  - [ ] 图片点击放大预览
  - [ ] 懒加载支持

### 性能优化
- [ ] 虚拟化支持（超长文档）
- [ ] 渲染性能分析工具
- [ ] 内存使用优化
- [ ] 异步渲染选项

---

## 🎯 v0.3.0 - 高级渲染特性

### 数学公式
- [ ] LaTeX 数学公式渲染
  - [ ] 集成 WpfMath 或类似库
  - [ ] 行内公式 `$...$`
  - [ ] 块级公式 `$$...$$`
  - [ ] 公式复制功能

### 图表支持
- [ ] Mermaid 图表渲染
  - [ ] 流程图
  - [ ] 时序图
  - [ ] 甘特图
  - [ ] 类图
- [ ] PlantUML 支持（可选）

### 扩展 API
- [ ] 自定义渲染器插件系统
  - [ ] `IMarkdownExtension` 接口
  - [ ] 渲染器注册机制
  - [ ] 示例：表情符号渲染器
  - [ ] 示例：任务列表渲染器
- [ ] Markdown 预处理钩子
- [ ] 后处理钩子（DOM 操作）

### Markdown 扩展语法
- [ ] 任务列表 `- [ ]` / `- [x]`
- [ ] 脚注支持
- [ ] 定义列表
- [ ] 表情符号 `:emoji:`
- [ ] 上标/下标
- [ ] 高亮标记 `==text==`

---

## 📦 v0.4.0 - 发布与生态

### NuGet 发布
- [ ] 准备 NuGet 包元数据
- [ ] 创建 Logo 和图标
- [ ] 编写发布说明
- [ ] 发布到 NuGet.org
- [ ] 配置 CI/CD（GitHub Actions）

### 文档网站
- [ ] 使用 Docusaurus 或 VitePress 创建文档站点
- [ ] API 参考文档
- [ ] 更多使用示例
- [ ] 最佳实践指南
- [ ] 性能调优指南

### 示例项目
- [ ] 创建独立的示例解决方案
- [ ] AI 聊天应用示例
- [ ] Markdown 编辑器示例
- [ ] 文档查看器示例

### 质量保证
- [ ] 单元测试（目标覆盖率 80%）
- [ ] 性能基准测试
- [ ] 内存泄漏测试
- [ ] UI 自动化测试

---

## 🐛 Bug 修复与改进

### 已知问题
- 当前无已知严重 bug

### 改进建议
- [ ] 优化超长单行文本的处理
- [ ] 改进表格响应式布局
- [ ] 增强滚动性能（大文档）
- [ ] 支持 Right-to-Left (RTL) 文本
- [ ] 辅助功能（无障碍）改进

---

## 🔧 技术债务

- [ ] 添加 XML 文档注释到所有公共 API
- [ ] 创建 Analyzer 和 Source Generator（可选）
- [ ] 审查并优化异常处理
- [ ] 标准化日志记录（ILogger）
- [ ] 代码风格统一（EditorConfig）

---

## 💡 未来想法（待评估）

- [ ] 支持移动平台（MAUI/Avalonia）
- [ ] 实时协作编辑支持
- [ ] Markdown 导出功能（PDF, HTML, DOCX）
- [ ] AI 辅助的 Markdown 建议
- [ ] 集成 spell checker
- [ ] 支持自定义快捷键

---

## ✅ 已完成（v0.2.0）

- ✅ 项目重命名：MarkdView.Core → MarkdView
- ✅ Extensions 和 Samples 改为文件夹结构
- ✅ 创建 `Themes/Light.xaml` - 完整的浅色主题资源字典
- ✅ 创建 `Themes/Dark.xaml` - 完整的深色主题资源字典
- ✅ 创建 `Themes/HighContrast.xaml` - 高对比度主题
- ✅ 添加 `ThemeManager` 类用于动态切换主题
- ✅ 主题预览示例（在 Samples/Themes/ 中）
- ✅ 添加代码块复制按钮
  - ✅ 创建 `CodeBlockControl` 自定义控件
  - ✅ 复制到剪贴板功能
  - ✅ 复制成功提示动画
  - ✅ 鼠标悬停显示按钮
- ✅ 集成 CodeBlockControl 到 CodeBlockRenderer
- ✅ 支持更多语言语法高亮：
  - ✅ Rust
  - ✅ Swift
  - ✅ Go
- ✅ 链接点击事件处理
  - ✅ `LinkClicked` 事件
  - ✅ 自定义链接处理器
- ✅ 创建完整的示例文件和主题切换示例
- ✅ 更新所有文档和版本信息
- ✅ 在 MinoChat 中测试所有功能集成

## ✅ 已完成（v0.1.0）

- ✅ 创建 MarkdView.Core 项目结构
- ✅ 迁移 MarkdownViewer 核心代码
- ✅ 提取渲染器到独立类
- ✅ 设计公共 API 和依赖属性
- ✅ 编写 README 和示例文档
- ✅ 在 MinoChat.Ui 中引用 MarkdView.Core
- ✅ 更新 MinoChat 使用新的 MarkdView 控件
- ✅ 构建并测试项目
- ✅ 创建交互式示例窗口（BasicUsage.xaml）
- ✅ 实现基础语法高亮（5+ 语言）
- ✅ 流式渲染支持
- ✅ 主题资源引用系统
- ✅ 编写 CHANGELOG.md

---

**最后更新**: 2025-11-16
**当前版本**: v0.2.0
**下一里程碑**: v0.3.0（高级渲染特性）

## 贡献指南

如果你想贡献代码或提出建议：

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

**优先级标签**:
- 🔴 高优先级 - 核心功能/严重 bug
- 🟡 中优先级 - 增强功能/性能优化
- 🟢 低优先级 - 改进/美化/实验性功能
