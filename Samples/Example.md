# MarkdView 示例

一个现代化的 WPF Markdown 渲染控件，支持语法高亮和主题切换。

## 基本使用

```xml
<markd:MarkdownViewer Markdown="{Binding Content}" Theme="{Binding Theme}" />
```

## 功能展示

### 标题

# 一级标题
## 二级标题
### 三级标题

### 文本样式

这是**粗体文本**，这是*斜体文本*，这是~~删除线~~。

### 列表

- 无序列表项 1
- 无序列表项 2
  - 嵌套项 2.1
  - 嵌套项 2.2

1. 有序列表项 1
2. 有序列表项 2
3. 有序列表项 3

### 引用

> 这是一段引用文本。
> 可以跨越多行。

### 代码

内联代码：`var example = "Hello World";`

代码块：

```csharp
public class Example
{
    public void Method()
    {
        var result = "Hello MarkdView!";
        Console.WriteLine(result);
    }
}
```

```python
def hello():
    print("Hello MarkdView!")
    return True
```

```javascript
const greeting = "Hello MarkdView!";
console.log(greeting);
```

### 链接

[MarkdView GitHub](https://github.com/your-repo)

### 表格

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| Markdown | string | "" | Markdown 文本 |
| Theme | ThemeMode | Dark | 主题模式 |
| EnableSyntaxHighlighting | bool | true | 语法高亮 |
| FontSize | double | 14.0 | 字体大小 |

### 分隔线

---

## 主要特性

- ✅ 浅色/深色主题
- ✅ 代码语法高亮
- ✅ 流式渲染
- ✅ MVVM 支持
