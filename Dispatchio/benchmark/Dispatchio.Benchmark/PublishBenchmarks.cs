using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dispatchio.Strategies;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatchio.Benchmark;

/// <summary>
/// Compares EventBus's dispatch cost against MediatR for notification publish with a growing
/// number of handlers. Run with: dotnet run -c Release --project benchmarks/EventBus.Benchmarks
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
public class PublishBenchmarks
{
    private IServiceProvider _eventBusProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [Params(1, 5, 20)]
    public int HandlerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var eventBusServices = new ServiceCollection();
        eventBusServices.AddScoped<Abstractions.IEventPublisher, EventPublisher>();
        eventBusServices.AddSingleton<Abstractions.IPublishStrategy, ForeachAwaitPublishStrategy>();
        for (var i = 0; i < HandlerCount; i++)
        {
            eventBusServices.AddTransient<Abstractions.INotificationHandler<BenchNotification>, BenchHandler>();
        }
        _eventBusProvider = eventBusServices.BuildServiceProvider();

        var mediatrServices = new ServiceCollection();
        mediatrServices.AddLogging();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PublishBenchmarks>());
        for (var i = 0; i < HandlerCount; i++)
        {
            mediatrServices.AddTransient<INotificationHandler<MediatRBenchNotification>, MediatRBenchHandler>();
        }
        _mediatrProvider = mediatrServices.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async Task EventBus_Publish()
    {
        using var scope = _eventBusProvider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<Abstractions.IEventPublisher>();
        await publisher.PublishAsync(new BenchNotification());
    }

    [Benchmark]
    public async Task MediatR_Publish()
    {
        using var scope = _mediatrProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await mediator.Publish(new MediatRBenchNotification());
    }
}

public sealed class BenchNotification : Abstractions.INotification;

public sealed class BenchHandler : Abstractions.INotificationHandler<BenchNotification>
{
    public Task HandleAsync(BenchNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class MediatRBenchNotification : INotification;

public sealed class MediatRBenchHandler : INotificationHandler<MediatRBenchNotification>
{
    public Task Handle(MediatRBenchNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
