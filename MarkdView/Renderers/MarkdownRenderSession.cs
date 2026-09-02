using System;
using System.Threading;

namespace MarkdView.Renderers;

/// <summary>
/// 一次 WPF 文档构建期间共享的异步会话。
/// 模型 renderer 与兼容 renderer 混合工作时，必须共用同一个图片预算和取消令牌。
/// </summary>
internal sealed class MarkdownRenderSession : IDisposable
{
    private int _imageCount;
    private int _disposed;

    public CancellationTokenSource Cancellation { get; } = new();

    public bool TryReserveImage(int maximum)
    {
        if (maximum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        var count = Interlocked.Increment(ref _imageCount);
        return count <= maximum;
    }

    public void Cancel()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            Cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Cancellation.Cancel();
        Cancellation.Dispose();
    }
}
