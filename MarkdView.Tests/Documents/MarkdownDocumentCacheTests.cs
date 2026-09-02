using Markdig;
using MarkdView.Documents;
using MarkdView.Renderers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace MarkdView.Tests.Documents;

public class MarkdownDocumentCacheTests
{
    [Fact]
    public void GetOrAdd_ShouldReuseSnapshotForSameSource()
    {
        var cache = new MarkdownDocumentCache(2);
        var calls = 0;
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());

        var first = cache.GetOrAdd("same", source =>
        {
            calls++;
            return renderer.ParseDocumentModel(source);
        });
        var second = cache.GetOrAdd("same", source =>
        {
            calls++;
            return renderer.ParseDocumentModel(source);
        });

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_ShouldEvictLeastRecentlyUsedSource()
    {
        var cache = new MarkdownDocumentCache(2);
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());
        var calls = 0;

        MarkdownDocumentModel Parse(string source)
        {
            calls++;
            return renderer.ParseDocumentModel(source);
        }

        cache.GetOrAdd("first", Parse);
        cache.GetOrAdd("second", Parse);
        cache.GetOrAdd("first", Parse);
        cache.GetOrAdd("third", Parse);
        cache.GetOrAdd("second", Parse);

        Assert.Equal(4, calls);
        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGet("second", out _));
        Assert.True(cache.TryGet("third", out _));
        Assert.False(cache.TryGet("first", out _));
    }

    [Fact]
    public async Task GetOrAdd_ShouldSingleFlightConcurrentParses()
    {
        var cache = new MarkdownDocumentCache(2);
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());
        var calls = 0;
        var models = new ConcurrentBag<MarkdownDocumentModel>();

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => models.Add(cache.GetOrAdd("concurrent", source =>
            {
                Interlocked.Increment(ref calls);
                Thread.Sleep(40);
                return renderer.ParseDocumentModel(source);
            }))));

        await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.Equal(8, models.Count);
        Assert.All(models, model => Assert.Same(models.First(), model));
    }

    [Fact]
    public async Task Clear_ShouldNotReinsertAnInFlightSnapshot()
    {
        var cache = new MarkdownDocumentCache(2);
        var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var pending = Task.Run(() => cache.GetOrAdd("stale", source =>
        {
            started.Set();
            release.Wait(TimeSpan.FromSeconds(2));
            return renderer.ParseDocumentModel(source);
        }));

        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        cache.Clear();
        release.Set();
        await pending;

        Assert.Equal(0, cache.Count);
    }
}
