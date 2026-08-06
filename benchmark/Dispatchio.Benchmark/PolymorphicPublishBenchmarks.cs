using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dispatchio.Strategies;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatchio.Benchmark;

/// <summary>
/// Measures the cost of the EnablePolymorphicDispatch option. Publishes a derived notification
/// and compares:
///  - Dispatchio with polymorphic dispatch disabled (exact-type, reflection-free fast path),
///  - Dispatchio with polymorphic dispatch enabled (runtime-type + base class + interface handlers),
///  - MediatR (which resolves by the compile-time notification type).
/// Note: handler *work* per publish differs by design — the polymorphic run also invokes the
/// base-class and interface handlers, which is exactly the overhead being measured.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
public class PolymorphicPublishBenchmarks
{
    private IServiceProvider _exactProvider = null!;
    private IServiceProvider _polymorphicProvider = null!;
    private IServiceProvider _mediatrProvider = null!;

    [Params(1, 5, 20)]
    public int HandlerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _exactProvider = BuildDispatchioProvider(enablePolymorphicDispatch: false);
        _polymorphicProvider = BuildDispatchioProvider(enablePolymorphicDispatch: true);

        var mediatrServices = new ServiceCollection();
        mediatrServices.AddLogging();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PolymorphicPublishBenchmarks>());
        for (var i = 0; i < HandlerCount; i++)
        {
            mediatrServices.AddTransient<INotificationHandler<MediatRDerivedNotification>, MediatRDerivedHandler>();
            mediatrServices.AddTransient<INotificationHandler<MediatRBaseNotification>, MediatRBaseHandler>();
        }
        _mediatrProvider = mediatrServices.BuildServiceProvider();
    }

    private IServiceProvider BuildDispatchioProvider(bool enablePolymorphicDispatch)
    {
        var services = new ServiceCollection();
        services.AddScoped<Abstractions.IEventPublisher>(sp => new EventPublisher(
            sp,
            sp.GetRequiredService<Abstractions.IPublishStrategy>(),
            enablePolymorphicDispatch));
        services.AddSingleton<Abstractions.IPublishStrategy, ForeachAwaitPublishStrategy>();
        for (var i = 0; i < HandlerCount; i++)
        {
            services.AddTransient<Abstractions.INotificationHandler<DerivedBenchNotification>, DerivedBenchHandler>();
            services.AddTransient<Abstractions.INotificationHandler<BaseBenchNotification>, BaseBenchHandler>();
        }
        return services.BuildServiceProvider();
    }

    [Benchmark(Baseline = true)]
    public async Task Dispatchio_Publish_ExactDispatch()
    {
        using var scope = _exactProvider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<Abstractions.IEventPublisher>();
        await publisher.PublishAsync(new DerivedBenchNotification());
    }

    [Benchmark]
    public async Task Dispatchio_Publish_PolymorphicDispatch()
    {
        using var scope = _polymorphicProvider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<Abstractions.IEventPublisher>();
        await publisher.PublishAsync(new DerivedBenchNotification());
    }

    [Benchmark]
    public async Task MediatR_Publish()
    {
        using var scope = _mediatrProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await mediator.Publish(new MediatRDerivedNotification());
    }
}

public class BaseBenchNotification : Abstractions.INotification;

public sealed class DerivedBenchNotification : BaseBenchNotification;

public sealed class DerivedBenchHandler : Abstractions.INotificationHandler<DerivedBenchNotification>
{
    public Task HandleAsync(DerivedBenchNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class BaseBenchHandler : Abstractions.INotificationHandler<BaseBenchNotification>
{
    public Task HandleAsync(BaseBenchNotification notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class MediatRBaseNotification : INotification;

public sealed class MediatRDerivedNotification : MediatRBaseNotification;

public sealed class MediatRDerivedHandler : INotificationHandler<MediatRDerivedNotification>
{
    public Task Handle(MediatRDerivedNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class MediatRBaseHandler : INotificationHandler<MediatRBaseNotification>
{
    public Task Handle(MediatRBaseNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

