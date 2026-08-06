using Dispatchio;
using Dispatchio.Abstractions;
using Dispatchio.Strategies;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.TestFixtures;

namespace UnitTests;

public class EventPublisherTests
{
    private static IServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishAsync_WithNoHandlersRegistered_DoesNotThrow()
    {
        var provider = BuildProvider(s => s.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>());
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        var act = () => publisher.PublishAsync(new SampleNotification("x"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithSingleHandler_InvokesIt()
    {
        var handler = new RecordingHandler();
        var provider = BuildProvider(s =>
        {
            s.AddSingleton<INotificationHandler<SampleNotification>>(handler);
            s.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        });
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        await publisher.PublishAsync(new SampleNotification("hello"));

        handler.Received.Should().ContainSingle().Which.Should().Be("hello");
    }

    [Fact]
    public async Task PublishAsync_WithMultipleHandlers_InvokesAllOfThem()
    {
        var first = new RecordingHandler();
        var second = new SecondRecordingHandler();
        var provider = BuildProvider(s =>
        {
            s.AddSingleton<INotificationHandler<SampleNotification>>(first);
            s.AddSingleton<INotificationHandler<SampleNotification>>(second);
            s.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        });
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        await publisher.PublishAsync(new SampleNotification("payload"));

        first.Received.Should().ContainSingle().Which.Should().Be("payload");
        second.Received.Should().ContainSingle().Which.Should().Be("payload");
    }

    [Fact]
    public async Task PublishAsync_OnlyInvokesHandlersForTheMatchingNotificationType()
    {
        var sampleHandler = new RecordingHandler();
        var provider = BuildProvider(s =>
        {
            s.AddSingleton<INotificationHandler<SampleNotification>>(sampleHandler);
            s.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        });
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        // OtherNotification has no registered handler at all — must be a silent no-op.
        await publisher.PublishAsync(new OtherNotification(42));

        sampleHandler.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_PropagatesCancellationTokenToHandlers()
    {
        var handler = new CancellationAwareHandler();
        var provider = BuildProvider(s =>
        {
            s.AddSingleton<INotificationHandler<SampleNotification>>(handler);
            s.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        });
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // ForeachAwaitPublishStrategy checks the token before each call and throws instead of
        // invoking the handler once cancellation has been requested.
        var act = () => publisher.PublishAsync(new SampleNotification("x"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.ReceivedCancelledToken.Should().BeFalse("the handler should never run once cancellation was requested");
    }

    [Fact]
    public async Task PublishAsync_WithNullNotification_ThrowsArgumentNullException()
    {
        var provider = BuildProvider(s => s.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>());
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        var act = () => publisher.PublishAsync<SampleNotification>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
