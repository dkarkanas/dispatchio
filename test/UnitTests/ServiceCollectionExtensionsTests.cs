using Dispatchio;
using Dispatchio.Abstractions;
using Dispatchio.Strategies;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.TestFixtures;

namespace UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEventBus_RegistersIEventPublisher()
    {
        var services = new ServiceCollection();

        services.AddDispatchio(cfg => cfg.RegisterHandlersFromAssemblyContaining<RecordingHandler>());

        var provider = services.BuildServiceProvider();
        provider.GetService<IEventPublisher>().Should().NotBeNull();
    }

    [Fact]
    public void AddEventBus_ScansAssemblyAndRegistersAllHandlerImplementations()
    {
        var services = new ServiceCollection();

        services.AddDispatchio(cfg => cfg.RegisterHandlersFromAssemblyContaining<RecordingHandler>());

        var provider = services.BuildServiceProvider();
        var handlers = provider.GetServices<INotificationHandler<SampleNotification>>().ToList();

        // RecordingHandler, SecondRecordingHandler, ThrowingHandler, DelayedThrowingHandler,
        // CancellationAwareHandler all implement INotificationHandler<SampleNotification> in
        // the test fixtures assembly.
        handlers.Should().HaveCountGreaterThanOrEqualTo(5);
        handlers.Select(h => h.GetType()).Should().Contain(typeof(RecordingHandler));
        handlers.Select(h => h.GetType()).Should().Contain(typeof(SecondRecordingHandler));
    }

    [Fact]
    public void AddEventBus_DefaultsToForeachAwaitStrategy()
    {
        var services = new ServiceCollection();

        services.AddDispatchio(cfg => cfg.RegisterHandlersFromAssemblyContaining<RecordingHandler>());

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPublishStrategy>().Should().BeOfType<ForeachAwaitPublishStrategy>();
    }

    [Fact]
    public void AddEventBus_UseStrategy_OverridesDefaultStrategy()
    {
        var services = new ServiceCollection();

        services.AddDispatchio(cfg =>
        {
            cfg.RegisterHandlersFromAssemblyContaining<RecordingHandler>();
            cfg.UseStrategy<WhenAllPublishStrategy>();
        });

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IPublishStrategy>().Should().BeOfType<WhenAllPublishStrategy>();
    }

    [Fact]
    public void AddEventBus_WithHandlerLifetime_RegistersHandlersWithThatLifetime()
    {
        var services = new ServiceCollection();

        services.AddDispatchio(cfg =>
        {
            cfg.RegisterHandlersFromAssemblyContaining<RecordingHandler>();
            cfg.WithHandlerLifetime(ServiceLifetime.Singleton);
        });

        var descriptor = services.Single(d =>
            d.ServiceType == typeof(INotificationHandler<SampleNotification>) &&
            d.ImplementationType == typeof(RecordingHandler));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public async Task Publisher_ResolvesHandlersFromTheSameScopeAsTheCaller()
    {
        // Regression test for the classic MediatR footgun: a scoped dependency (simulated here by
        // ScopedMarker) shared between the code that publishes and the handler that receives the
        // notification must be the *same instance*, proving handlers resolve from the caller's
        // scope rather than a freshly created / root-provider scope. Manual registration only
        // (not assembly scanning) so there's exactly one handler instance per publish.
        var services = new ServiceCollection();
        services.AddScoped<ScopedMarker>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        services.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        services.AddScoped<INotificationHandler<SampleNotification>, ScopeCheckingHandler>();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var marker = scope.ServiceProvider.GetRequiredService<ScopedMarker>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new SampleNotification("scope-test"));

        // Resolving the scoped handler again from the same scope must yield the same instance
        // that ran during PublishAsync, and it must have observed the same ScopedMarker instance.
        var handler = (ScopeCheckingHandler)scope.ServiceProvider
            .GetRequiredService<INotificationHandler<SampleNotification>>();

        handler.ObservedMarker.Should().BeSameAs(marker);
    }

    private sealed class ScopedMarker
    {
    }

    private sealed class ScopeCheckingHandler : INotificationHandler<SampleNotification>
    {
        private readonly ScopedMarker _marker;
        public ScopedMarker? ObservedMarker { get; private set; }

        public ScopeCheckingHandler(ScopedMarker marker) => _marker = marker;

        public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
        {
            ObservedMarker = _marker;
            return Task.CompletedTask;
        }
    }
}
