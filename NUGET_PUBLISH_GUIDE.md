# MarkdView NuGet 发布指南

## 📋 发布前检查清单

### ✅ 已完成项
- [x] NuGet 元数据配置完整（.csproj）
- [x] LICENSE 文件存在（MIT License）
- [x] README.md 文件存在
- [x] 依赖项配置正确（Emoji.Wpf + Markdig）

### ⚠️ 待完成项
- [ ] 添加项目图标/Logo（可选，但推荐）
- [ ] 验证所有文件打包正确
- [ ] 本地测试 NuGet 包
- [ ] 注册 NuGet.org 账号
- [ ] 获取 API Key
- [ ] 发布到 NuGet.org

---

## 🎨 步骤 1: 添加项目图标（推荐）

NuGet 包带图标会更专业，更容易识别。

### 选项 A：创建简单图标
1. 准备一个 64x64 或 128x128 的 PNG 图标
2. 保存为 `MarkdView/icon.png`
3. 修改 `.csproj` 添加：

```xml
<PropertyGroup>
  <!-- 其他配置... -->
  <PackageIcon>icon.png</PackageIcon>
</PropertyGroup>

<ItemGroup>
  <None Include="icon.png" Pack="true" PackagePath="\" />
</ItemGroup>
```

### 选项 B：使用 Emoji 作为图标（快速方案）
可以暂时跳过，后续更新时添加。

---

## 🔨 步骤 2: 生成 NuGet 包

### 方式 1：使用 dotnet CLI（推荐）

打开终端，进入项目目录：

```bash
cd MarkdView

# 清理之前的构建
dotnet clean

# Release 模式构建并打包
dotnet pack -c Release -o ./bin/NuGet

# 检查生成的包
ls bin/NuGet
```

### 方式 2：使用 Visual Studio / Rider

1. 右键点击 `MarkdView` 项目
2. 选择 "Pack" 或 "打包"
3. 在 `bin/Release` 或 `bin/NuGet` 中找到 `.nupkg` 文件

---

## 🧪 步骤 3: 本地测试 NuGet 包

在发布前，务必先本地测试！

### 3.1 创建测试项目

```bash
# 创建测试解决方案（在项目根目录外）
mkdir TestMarkdView
cd TestMarkdView
dotnet new wpf -n TestApp
```

### 3.2 添加本地 NuGet 源

```bash
# 添加本地 NuGet 包源
dotnet nuget add source "C:\Users\zhouh\RiderProjects\MarkView\MarkdView\bin\NuGet" --name "MarkdView-Local"

# 验证源已添加
dotnet nuget list source
```

### 3.3 安装并测试包

```bash
cd TestApp
dotnet add package MarkdView --version 1.0.0 --source "MarkdView-Local"
```

在 `MainWindow.xaml` 中测试：

```xaml
<Window xmlns:markd="clr-namespace:MarkdView.Controls;assembly=MarkdView">
    <markd:MarkdownViewer Markdown="# Hello MarkdView!" />
</Window>
```

运行测试项目，确保一切正常！

### 3.4 清理测试源（可选）

```bash
dotnet nuget remove source "MarkdView-Local"
```

---

## 🌐 步骤 4: 注册 NuGet.org 账号

### 4.1 注册账号
1. 访问 https://www.nuget.org/
2. 点击右上角 "Sign in" → "Register"
3. 使用 Microsoft 账号或 Email 注册

### 4.2 获取 API Key
1. 登录后，点击右上角用户名 → "API Keys"
2. 点击 "Create" 创建新 API Key
3. 配置：
   - **Key Name**: `MarkdView-Publish`
   - **Select Scopes**:
     - ✅ Push new packages and package versions
     - ✅ Push symbols for existing packages
   - **Glob Pattern**: `MarkdView*` （只允许推送 MarkdView 相关包）
   - **Expires in**: 选择 365 days 或 "Never expire"
4. 点击 "Create" 并**立即复制** API Key（只会显示一次！）

**⚠️ 安全提示：** 妥善保管 API Key，不要上传到 Git！

---

## 🚀 步骤 5: 发布到 NuGet.org

### 方式 1：使用 dotnet CLI（推荐）

```bash
cd MarkdView

# 发布到 NuGet.org
dotnet nuget push bin/NuGet/MarkdView.1.0.0.nupkg \
  --api-key YOUR_API_KEY_HERE \
  --source https://api.nuget.org/v3/index.json

# 示例（替换为您的实际 API Key）
dotnet nuget push bin/NuGet/MarkdView.1.0.0.nupkg \
  --api-key oy2abc...xyz \
  --source https://api.nuget.org/v3/index.json
```

### 方式 2：通过网页上传

1. 访问 https://www.nuget.org/packages/manage/upload
2. 选择 `MarkdView.1.0.0.nupkg` 文件
3. 点击 "Upload"
4. 验证包信息并提交

---

## ⏳ 步骤 6: 等待审核和索引

### 6.1 验证流程
- NuGet.org 会自动验证包（通常 1-5 分钟）
- 检查是否包含恶意代码或违规内容
- 验证元数据完整性

### 6.2 索引时间
- **初次上传**: 可能需要 10-30 分钟才能在搜索中出现
- **后续更新**: 通常 5-15 分钟

### 6.3 验证发布成功

访问以下链接检查：
```
https://www.nuget.org/packages/MarkdView/
```

或使用 CLI 搜索：
```bash
dotnet search MarkdView
```

---

## 📦 步骤 7: 测试已发布的包

### 7.1 从 NuGet.org 安装

```bash
# 创建新测试项目
dotnet new wpf -n FinalTest
cd FinalTest

# 从 NuGet.org 安装
dotnet add package MarkdView

# 验证安装
dotnet list package
```

### 7.2 运行测试

确保功能正常：
- ✅ Markdown 渲染正常
- ✅ 流式渲染工作
- ✅ 代码高亮显示
- ✅ Emoji 彩色渲染
- ✅ 主题切换功能

---

## 🔄 后续版本更新流程

### 更新版本号

编辑 `MarkdView.csproj`：

```xml
<PropertyGroup>
  <Version>1.0.1</Version>
  <PackageReleaseNotes>v1.0.1: 修复了 XXX bug，新增了 YYY 功能</PackageReleaseNotes>
</PropertyGroup>
```

### 重新打包并发布

```bash
# 清理 → 打包 → 发布
dotnet clean
dotnet pack -c Release -o ./bin/NuGet
dotnet nuget push bin/NuGet/MarkdView.1.0.1.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

---

## 🎯 快速执行脚本

为了方便后续发布，可以创建一个脚本：

### Windows PowerShell 脚本 (`publish.ps1`)

```powershell
# 设置参数
$Version = "1.0.0"
$ApiKey = Read-Host "请输入 NuGet API Key"

# 清理
Write-Host "清理项目..." -ForegroundColor Yellow
dotnet clean

# 打包
Write-Host "打包项目..." -ForegroundColor Yellow
dotnet pack -c Release -o ./bin/NuGet

# 发布
Write-Host "发布到 NuGet.org..." -ForegroundColor Yellow
dotnet nuget push "bin/NuGet/MarkdView.$Version.nupkg" `
  --api-key $ApiKey `
  --source https://api.nuget.org/v3/index.json

Write-Host "发布完成！" -ForegroundColor Green
Write-Host "请访问 https://www.nuget.org/packages/MarkdView/ 查看" -ForegroundColor Cyan
```

### 使用脚本

```powershell
cd MarkdView
.\publish.ps1
```

---

## ❗ 常见问题排查

### 问题 1: "Package with id 'MarkdView' already exists"
**原因**: NuGet.org 上已存在该包名
**解决**:
- 选择不同的包名（如 `MarkdView.WPF`）
- 或联系包所有者转让

### 问题 2: "Invalid API Key"
**原因**: API Key 错误或已过期
**解决**: 重新生成 API Key

### 问题 3: 包上传后搜索不到
**原因**: 索引尚未完成
**解决**: 等待 10-30 分钟，清除浏览器缓存

### 问题 4: 缺少 README 或 LICENSE
**原因**: 文件路径配置错误
**解决**: 检查 `.csproj` 中的路径：
```xml
<None Include="..\README.md" Pack="true" PackagePath="\" />
<None Include="..\LICENSE" Pack="true" PackagePath="\" />
```

### 问题 5: 依赖项未正确打包
**原因**: `PackageReference` 配置错误
**解决**: 确保依赖项在 `.csproj` 中正确声明

---

## 📊 发布后推广建议

### 1. 更新 GitHub README
添加 NuGet 徽章：

```markdown
[![NuGet](https://img.shields.io/nuget/v/MarkdView.svg)](https://www.nuget.org/packages/MarkdView/)
[![Downloads](https://img.shields.io/nuget/dt/MarkdView.svg)](https://www.nuget.org/packages/MarkdView/)
```

### 2. 创建 GitHub Release
- 在 GitHub 创建 v1.0.0 Release
- 上传 `.nupkg` 文件作为附件
- 编写 Release Notes

### 3. 社区分享
- 在 Reddit r/dotnet 分享
- 在 Twitter/X 发布
- 在相关 WPF/Markdown 论坛介绍

---

## 🎉 恭喜！

完成上述步骤后，您的 MarkdView 包就成功发布到 NuGet.org 了！

其他开发者可以通过以下方式使用：

```bash
dotnet add package MarkdView
```

或在 Visual Studio 的 NuGet 包管理器中搜索 "MarkdView"。

---

**最后更新**: 2025-11-16
**作者**: MarkdView Team
