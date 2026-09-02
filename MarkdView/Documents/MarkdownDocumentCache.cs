using System;
using System.Collections.Generic;

namespace MarkdView.Documents;

/// <summary>
/// 有界、线程安全的 Markdown 文档快照缓存。
/// 缓存键使用完整源文本，避免仅依赖哈希时的碰撞风险；快照自身仍保留 SHA-256 内容哈希供诊断和后续增量渲染使用。
/// </summary>
public sealed class MarkdownDocumentCache
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Lazy<MarkdownDocumentModel>> _inflight = new(StringComparer.Ordinal);
    private readonly LinkedList<CacheEntry> _lru = new();
    private readonly int _capacity;
    private long _epoch;

    public MarkdownDocumentCache(int capacity = 32)
    {
        if (capacity is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "缓存容量必须在 1 到 1024 之间。");
        }

        _capacity = capacity;
    }

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(string? markdown, out MarkdownDocumentModel model)
    {
        var source = markdown ?? string.Empty;
        lock (_gate)
        {
            if (_entries.TryGetValue(source, out var node))
            {
                Touch(node);
                model = node.Value.Model;
                return true;
            }
        }

        model = null!;
        return false;
    }

    /// <summary>
    /// 命中时复用快照，未命中时调用 parser 创建并缓存快照。
    /// parser 不在锁内执行，避免大型文档解析阻塞其他线程；同一源文本的并发请求共享一个进行中的解析任务。
    /// </summary>
    public MarkdownDocumentModel GetOrAdd(
        string? markdown,
        Func<string, MarkdownDocumentModel> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        var source = markdown ?? string.Empty;

        Lazy<MarkdownDocumentModel> pending;
        long requestEpoch;
        lock (_gate)
        {
            requestEpoch = _epoch;
            if (_entries.TryGetValue(source, out var cached))
            {
                Touch(cached);
                return cached.Value.Model;
            }

            if (!_inflight.TryGetValue(source, out pending!))
            {
                pending = new Lazy<MarkdownDocumentModel>(
                    () => ParseAndValidate(source, parser),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
                _inflight[source] = pending;
            }
        }

        MarkdownDocumentModel parsed;
        try
        {
            parsed = pending.Value;
        }
        catch
        {
            lock (_gate)
            {
                if (_inflight.TryGetValue(source, out var current) && ReferenceEquals(current, pending))
                {
                    _inflight.Remove(source);
                }
            }

            throw;
        }

        lock (_gate)
        {
            if (requestEpoch != _epoch)
            {
                if (_inflight.TryGetValue(source, out var invalidated) && ReferenceEquals(invalidated, pending))
                {
                    _inflight.Remove(source);
                }

                return parsed;
            }

            _inflight.Remove(source);
            if (_entries.TryGetValue(source, out var existing))
            {
                Touch(existing);
                return existing.Value.Model;
            }

            var node = _lru.AddFirst(new CacheEntry(source, parsed));
            _entries[source] = node;
            while (_entries.Count > _capacity && _lru.Last != null)
            {
                var evicted = _lru.Last;
                _lru.RemoveLast();
                _entries.Remove(evicted.Value.Source);
            }
        }

        return parsed;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _epoch++;
            _entries.Clear();
            _lru.Clear();
            _inflight.Clear();
        }
    }

    private static MarkdownDocumentModel ParseAndValidate(
        string source,
        Func<string, MarkdownDocumentModel> parser)
    {
        var parsed = parser(source) ?? throw new InvalidOperationException("Markdown parser 返回了 null 文档模型。");
        if (!string.Equals(parsed.SourceText, source, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Markdown parser 返回的模型源码与请求不一致。");
        }

        return parsed;
    }

    private void Touch(LinkedListNode<CacheEntry> node)
    {
        if (node.List == _lru && node != _lru.First)
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
        }
    }

    private sealed record CacheEntry(string Source, MarkdownDocumentModel Model);
}
