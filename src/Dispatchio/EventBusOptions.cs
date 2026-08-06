using System.Reflection;
using Dispatchio.Abstractions;
using Dispatchio.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatchio;

/// <summary>
/// Configuration surface passed to <c>AddEventBus</c>. Used to register handler assemblies and
/// choose a publish strategy.
/// </summary>
public sealed class EventBusOptions
{
    internal List<Assembly> AssembliesToScan { get; } = [];
    internal Type StrategyType { get; private set; } = typeof(ForeachAwaitPublishStrategy);
    internal ServiceLifetime HandlerLifetime { get; private set; } = ServiceLifetime.Transient;
    internal bool PolymorphicDispatchEnabled { get; private set; }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TMarker"/> for
    /// <see cref="INotificationHandler{TNotification}"/> implementations and registers them.
    /// </summary>
    public EventBusOptions RegisterHandlersFromAssemblyContaining<TMarker>()
        => RegisterHandlersFromAssembly(typeof(TMarker).Assembly);

    /// <summary>
    /// Scans the given assembly for <see cref="INotificationHandler{TNotification}"/>
    /// implementations and registers them. Can be called multiple times to scan several assemblies.
    /// </summary>
    public EventBusOptions RegisterHandlersFromAssembly(Assembly assembly)
    {
#if NETSTANDARD2_0
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
#else
        ArgumentNullException.ThrowIfNull(assembly);
#endif
        AssembliesToScan.Add(assembly);
        return this;
    }

    /// <summary>
    /// Sets the <see cref="IPublishStrategy"/> implementation to use. Defaults to
    /// <see cref="ForeachAwaitPublishStrategy"/> (sequential, first-exception-stops-the-rest).
    /// </summary>
    public EventBusOptions UseStrategy<TStrategy>()
        where TStrategy : class, IPublishStrategy
    {
        StrategyType = typeof(TStrategy);
        return this;
    }

    /// <summary>
    /// Sets the DI lifetime used when registering discovered handler implementations.
    /// Defaults to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public EventBusOptions WithHandlerLifetime(ServiceLifetime lifetime)
    {
        HandlerLifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Enables polymorphic dispatch: when a notification is published, handlers registered for its
    /// base notification types and implemented notification interfaces are invoked as well,
    /// not only handlers for the exact runtime type. Disabled by default.
    /// </summary>
    /// <param name="enabled">Whether polymorphic dispatch should be enabled.</param>
    public EventBusOptions EnablePolymorphicDispatch(bool enabled = true)
    {
        PolymorphicDispatchEnabled = enabled;
        return this;
    }
}