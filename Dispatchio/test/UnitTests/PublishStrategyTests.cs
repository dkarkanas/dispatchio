using Dispatchio.Strategies;
using FluentAssertions;

namespace UnitTests;

public class PublishStrategyTests
{
    [Fact]
    public async Task ForeachAwaitPublishStrategy_RunsCallsInOrder()
    {
        var order = new List<int>();
        var calls = new List<Func<CancellationToken, Task>>
        {
            _ => { order.Add(1); return Task.CompletedTask; },
            _ => { order.Add(2); return Task.CompletedTask; },
            _ => { order.Add(3); return Task.CompletedTask; },
        };

        await new ForeachAwaitPublishStrategy().PublishAsync(calls, CancellationToken.None);

        order.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ForeachAwaitPublishStrategy_StopsAtFirstException_SubsequentHandlersDoNotRun()
    {
        var ran = new List<int>();
        var calls = new List<Func<CancellationToken, Task>>
        {
            _ => { ran.Add(1); return Task.CompletedTask; },
            _ => throw new InvalidOperationException("boom"),
            _ => { ran.Add(3); return Task.CompletedTask; },
        };

        var act = () => new ForeachAwaitPublishStrategy().PublishAsync(calls, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        ran.Should().Equal(1); // handler 3 never ran
    }

    [Fact]
    public async Task WhenAllPublishStrategy_RunsAllCallsEvenIfIndependent()
    {
        var ran = new bool[3];
        var calls = new List<Func<CancellationToken, Task>>
        {
            async _ => { await Task.Delay(5); ran[0] = true; },
            async _ => { await Task.Delay(1); ran[1] = true; },
            _ => { ran[2] = true; return Task.CompletedTask; },
        };

        await new WhenAllPublishStrategy().PublishAsync(calls, CancellationToken.None);

        ran.Should().AllBeEquivalentTo(true);
    }

    [Fact]
    public async Task WhenAllPublishStrategy_WithSingleFailingHandler_ThrowsThatException()
    {
        var calls = new List<Func<CancellationToken, Task>>
        {
            _ => Task.CompletedTask,
            _ => throw new InvalidOperationException("solo failure"),
        };

        var act = () => new WhenAllPublishStrategy().PublishAsync(calls, CancellationToken.None);

        (await act.Should().ThrowAsync<Exception>())
            .Where(e => e is InvalidOperationException || e is AggregateException);
    }

    [Fact]
    public async Task WhenAllPublishStrategy_WithMultipleFailingHandlers_AggregatesAllExceptions()
    {
        var calls = new List<Func<CancellationToken, Task>>
        {
            async _ => { await Task.Delay(5); throw new InvalidOperationException("first"); },
            async _ => { await Task.Delay(5); throw new InvalidOperationException("second"); },
            _ => Task.CompletedTask,
        };

        var act = () => new WhenAllPublishStrategy().PublishAsync(calls, CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<AggregateException>();
        assertion.Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task WhenAllPublishStrategy_AllHandlersStartConcurrently_NotWaitingOnEachOtherSequentially()
    {
        // A handler that finishes fast should not be blocked behind a slower one when using
        // WhenAll — this differentiates it from ForeachAwaitPublishStrategy.
        var fastCompletedAt = DateTime.MinValue;
        var calls = new List<Func<CancellationToken, Task>>
        {
            async _ => { await Task.Delay(100); },
            _ => { fastCompletedAt = DateTime.UtcNow; return Task.CompletedTask; },
        };

        var start = DateTime.UtcNow;
        await new WhenAllPublishStrategy().PublishAsync(calls, CancellationToken.None);

        (fastCompletedAt - start).Should().BeLessThan(TimeSpan.FromMilliseconds(90));
    }
}
