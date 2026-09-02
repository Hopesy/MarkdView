using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;
using MarkdView.Documents;
using MarkdView.Parsing;

namespace MarkdView.Renderers;

/// <summary>
/// 管理 Markdown 解析/构建任务的取消和最新请求竞争。
/// FlowDocument 始终通过目标 Dispatcher 创建，过期任务返回 null。
/// </summary>
public sealed class MarkdownRenderCoordinator : IDisposable
{
    private readonly IMarkdownDocumentParser _documentParser;
    private readonly IMarkdownFlowDocumentRenderer _renderer;
    private readonly Dispatcher _dispatcher;
    private readonly MarkdownDocumentCache _documentCache;
    private readonly object _gate = new();
    private CancellationTokenSource? _activeCancellation;
    private long _generation;
    private bool _disposed;

    public MarkdownRenderCoordinator(
        IMarkdownDocumentRenderer renderer,
        Dispatcher dispatcher,
        MarkdownDocumentCache? documentCache = null)
        : this(renderer, renderer, dispatcher, documentCache)
    {
    }

    /// <summary>
    /// 使用独立的模型解析端口和 WPF 输出端口。
    /// </summary>
    public MarkdownRenderCoordinator(
        IMarkdownDocumentParser documentParser,
        IMarkdownFlowDocumentRenderer renderer,
        Dispatcher dispatcher,
        MarkdownDocumentCache? documentCache = null)
    {
        _documentParser = documentParser ?? throw new ArgumentNullException(nameof(documentParser));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _documentCache = documentCache ?? new MarkdownDocumentCache();
    }

    /// <summary>
    /// 只保留最新一次渲染请求。返回 null 表示请求已被替换、取消或 Dispatcher 已关闭。
    /// </summary>
    public async Task<FlowDocument?> RenderLatestAsync(string markdown, MarkdownRenderOptions options)
        => await RenderLatestAsync(markdown, options, TimeSpan.Zero).ConfigureAwait(false);

    /// <summary>
    /// 延迟提交最新渲染请求。延迟期间到达的新请求会取消当前等待，避免流式输入为每个字符启动解析。
    /// </summary>
    public async Task<FlowDocument?> RenderLatestAsync(
        string markdown,
        MarkdownRenderOptions options,
        TimeSpan debounce)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (debounce < TimeSpan.Zero || debounce == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce), debounce, "防抖时间不能为负数或无限时长。");
        }

        CancellationToken token;
        long generation;
        lock (_gate)
        {
            ThrowIfDisposed();
            _activeCancellation?.Cancel();
            _renderer.CancelPendingOperations();
            _activeCancellation?.Dispose();
            _activeCancellation = new CancellationTokenSource();
            token = _activeCancellation.Token;
            generation = ++_generation;
        }

        try
        {
            if (debounce > TimeSpan.Zero)
            {
                await Task.Delay(debounce, token).ConfigureAwait(false);
            }

            var documentModel = await Task.Run(
                () => _documentCache.GetOrAdd(markdown, _documentParser.Parse),
                token).ConfigureAwait(false);

            if (!IsCurrent(generation, token) || !IsDispatcherAlive())
            {
                return null;
            }

            if (_dispatcher.CheckAccess())
            {
                token.ThrowIfCancellationRequested();
                var document = IsCurrent(generation, token)
                    ? _renderer.ConvertDocumentToFlowDocument(documentModel, options)
                    : null;
                return IsCurrent(generation, token) ? document : null;
            }

            var renderedDocument = await _dispatcher.InvokeAsync(
                () =>
                {
                    token.ThrowIfCancellationRequested();
                    return IsCurrent(generation, token)
                        ? _renderer.ConvertDocumentToFlowDocument(documentModel, options)
                        : null;
                },
                DispatcherPriority.Background).Task.ConfigureAwait(false);
            return IsCurrent(generation, token) ? renderedDocument : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !IsDispatcherAlive())
        {
            return null;
        }
        catch (InvalidOperationException) when (!IsDispatcherAlive())
        {
            // Dispatcher 在排队和执行之间关闭时，当前结果已经无法安装，不应转化为用户可见的渲染错误。
            return null;
        }
    }

    /// <summary>
    /// 取消当前任务并阻止其结果安装。
    /// </summary>
    public void Cancel()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _generation++;
            _activeCancellation?.Cancel();
            _renderer.CancelPendingOperations();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            _activeCancellation?.Cancel();
            _activeCancellation?.Dispose();
            _activeCancellation = null;
            _renderer.CancelPendingOperations();
        }
    }

    private bool IsCurrent(long generation, CancellationToken token)
    {
        lock (_gate)
        {
            return !_disposed
                && generation == _generation
                && !token.IsCancellationRequested;
        }
    }

    private bool IsDispatcherAlive()
        => !_dispatcher.HasShutdownStarted
            && !_dispatcher.HasShutdownFinished
            && _dispatcher.Thread.IsAlive;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MarkdownRenderCoordinator));
        }
    }
}
