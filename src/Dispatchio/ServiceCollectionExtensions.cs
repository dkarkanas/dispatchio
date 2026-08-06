using Dispatchio.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatchio;

/// <summary>
/// 
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEventPublisher"/>, the chosen <see cref="IPublishStrategy"/>, and every
    /// <see cref="INotificationHandler{TNotification}"/> implementation found in the assemblies
    /// configured via <paramref name="configure"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddDispatchio(cfg =>
    /// {
    ///     cfg.RegisterHandlersFromAssemblyContaining&lt;OrderCreatedHandler&gt;();
    ///     cfg.UseStrategy&lt;WhenAllPublishStrategy&gt;();
    /// });
    /// </code>
    /// </example>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="configure">Callback used to configure assemblies to scan, publish strategy, and handler lifetime.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddDispatchio(this IServiceCollection services, Action<EventBusOptions> configure)
    {
#if NETSTANDARD2_0
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));
#else
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
#endif

        var options = new EventBusOptions();
        configure(options);

        // Publisher itself is scoped: in ASP.NET Core / generic host this ties handler resolution
        // to the same DI scope as whatever triggered the publish (e.g. same request, same
        // DbContext instance) instead of silently spinning up a fresh root-provider scope.
        services.AddScoped<IEventPublisher>(sp => new EventPublisher(
            sp,
            sp.GetRequiredService<IPublishStrategy>(),
            options.PolymorphicDispatchEnabled)
        );

        services.AddSingleton(typeof(IPublishStrategy), options.StrategyType);

        RegisterHandlers(services, options);

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, EventBusOptions options)
    {
        var handlerInterfaceType = typeof(INotificationHandler<>);

        foreach (var assembly in options.AssembliesToScan)
        {
            // Skip private nested types: they are implementation details of their declaring type
            // (e.g. test helpers) and are not intended to be discovered by assembly scanning.
            var candidateTypes = assembly.GetTypes()
                .Where(type => type is
                    {IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false, IsNestedPrivate: false});

            foreach (var type in candidateTypes)
            {
                var implementedInterfaces = type
                    .GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterfaceType);

                foreach (var handlerInterface in implementedInterfaces)
                {
                    services.Add(new ServiceDescriptor(handlerInterface, type, options.HandlerLifetime));
                }
            }
        }
    }
}