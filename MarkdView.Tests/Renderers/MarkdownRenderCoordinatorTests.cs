using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Markdig;
using MarkdView.Renderers;
using Xunit;

namespace MarkdView.Tests.Renderers;

public class MarkdownRenderCoordinatorTests
{
    [Fact]
    public void RenderLatestAsync_ShouldCreateFlowDocumentOnTargetDispatcher()
    {
        var document = RunOnStaDispatcher((dispatcher, coordinator) =>
            coordinator.RenderLatestAsync(
                "# Dispatcher",
                new MarkdownRenderOptions(new System.Windows.Media.FontFamily("Segoe UI"), 12)));

        Assert.NotNull(document);
        Assert.IsType<FlowDocument>(document);
    }

    [Fact]
    public void Cancel_ShouldMakeInFlightRequestReturnNull()
    {
        var document = RunOnStaDispatcher((dispatcher, coordinator) =>
        {
            var task = coordinator.RenderLatestAsync(
                "# Cancelled",
                new MarkdownRenderOptions(new System.Windows.Media.FontFamily("Segoe UI"), 12));
            coordinator.Cancel();
            return task;
        });

        Assert.Null(document);
    }

    [Fact]
    public void DebouncedRequest_ShouldYieldToNewerRequest()
    {
        var document = RunOnStaDispatcher((dispatcher, coordinator) =>
        {
            var first = coordinator.RenderLatestAsync(
                "# stale",
                new MarkdownRenderOptions(new System.Windows.Media.FontFamily("Segoe UI"), 12),
                TimeSpan.FromMilliseconds(150));

            Thread.Sleep(20);

            var second = coordinator.RenderLatestAsync(
                "# latest",
                new MarkdownRenderOptions(new System.Windows.Media.FontFamily("Segoe UI"), 12));
            return AssertLatestWinsAsync(first, second);
        });

        Assert.NotNull(document);
    }

    [Fact]
    public void DebouncedRequest_ShouldRejectNegativeDelay()
    {
        var document = RunOnStaDispatcher((dispatcher, coordinator) =>
            ExpectNegativeDelayAsync(() => coordinator.RenderLatestAsync(
                "# invalid",
                new MarkdownRenderOptions(new System.Windows.Media.FontFamily("Segoe UI"), 12),
                TimeSpan.FromMilliseconds(-1))));

        Assert.Null(document);
    }

    private static async Task<FlowDocument?> AssertLatestWinsAsync(
        Task<FlowDocument?> stale,
        Task<FlowDocument?> latest)
    {
        var latestDocument = await latest;
        Assert.Null(await stale);
        return latestDocument;
    }

    private static async Task<FlowDocument?> ExpectNegativeDelayAsync(Func<Task<FlowDocument?>> operation)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(operation);
        return null;
    }

    private static FlowDocument? RunOnStaDispatcher(
        Func<Dispatcher, MarkdownRenderCoordinator, Task<FlowDocument?>> operation)
    {
        FlowDocument? result = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                var renderer = new MarkdownRenderer(new MarkdownPipelineBuilder().Build());
                var wpfRenderer = new WpfFlowDocumentRenderer(renderer);
                using var coordinator = new MarkdownRenderCoordinator(renderer, wpfRenderer, dispatcher);
                var task = operation(dispatcher, coordinator);
                task.ContinueWith(
                    completed =>
                    {
                        try
                        {
                            result = completed.GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                        }
                        finally
                        {
                            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw error;
        }

        return result;
    }
}
