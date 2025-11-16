using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MarkdView.Examples;

/// <summary>
/// MarkdView 基础用法示例窗口
/// </summary>
public partial class BasicUsage : Window
{
    private DispatcherTimer? _streamingTimer;
    private string _fullStreamingText = @"# AI 流式输出演示

正在逐字生成 Markdown 内容...

## 代码生成示例

```csharp
public class StreamingDemo
{
    public async Task GenerateCodeAsync()
    {
        // 模拟 AI 逐步生成代码
        await Task.Delay(100);
        Console.WriteLine(""生成中..."");
    }
}
```

## 特性说明

- **防抖优化**: 50ms 间隔合并更新
- **性能优良**: CPU 占用 < 5%
- **流畅体验**: 实时渲染无卡顿

> 这是一个引用块，展示流式渲染的效果

| 指标 | 数值 |
|------|------|
| 延迟 | 50ms |
| CPU | <5% |
| 内存 | ~2MB |

---

**流式渲染完成!** 🎉
";
    private int _streamingIndex;

    public BasicUsage()
    {
        InitializeComponent();

        // 绑定按钮事件
        StartStreamingButton.Click += OnStartStreamingClick;
    }

    /// <summary>
    /// 开始流式渲染演示
    /// </summary>
    private void OnStartStreamingClick(object sender, RoutedEventArgs e)
    {
        // 重置状态
        _streamingIndex = 0;
        StreamingViewer.Markdown = "";
        StartStreamingButton.IsEnabled = false;

        // 创建计时器模拟流式输出
        _streamingTimer?.Stop();
        _streamingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30) // 模拟 AI 输出速度
        };

        _streamingTimer.Tick += (s, args) =>
        {
            // 每次添加 5-10 个字符
            var chunkSize = Math.Min(8, _fullStreamingText.Length - _streamingIndex);
            if (chunkSize > 0)
            {
                var chunk = _fullStreamingText.Substring(_streamingIndex, chunkSize);
                _streamingIndex += chunkSize;

                // 追加内容（触发流式渲染）
                StreamingViewer.Markdown += chunk;
            }
            else
            {
                // 完成
                _streamingTimer.Stop();
                StartStreamingButton.IsEnabled = true;
            }
        };

        _streamingTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _streamingTimer?.Stop();
        base.OnClosed(e);
    }
}
