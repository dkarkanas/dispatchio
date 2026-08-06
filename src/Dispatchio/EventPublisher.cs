using System.Collections.Concurrent;
using System.Reflection;
using Dispatchio.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatchio;

/// <summary>
/// Default <see cref="IEventPublisher"/> implementation. Resolves
/// <see cref="INotificationHandler{TNotification}"/> instances from the current
/// <see cref="IServiceProvider"/> scope and delegates execution to the configured
/// <see cref="IPublishStrategy"/>.
/// </summary>
public sealed class EventPublisher(
    IServiceProvider serviceProvider,
    IPublishStrategy strategy,
    bool enablePolymorphicDispatch = false
) : IEventPublisher
{
    // Caches the closed INotificationHandler<T> interfaces (and their HandleAsync method) that a
    // notification's runtime type maps to when polymorphic dispatch is enabled.
    private static readonly ConcurrentDictionary<Type, (Type HandlerInterface, MethodInfo HandleAsync)[]>
        PolymorphicHandlerCache = new();

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private readonly IPublishStrategy _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    /// <summary>
    /// Publishes the notification to every registered
    /// <see cref="INotificationHandler{TNotification}"/>, executing them according to the
    /// configured <see cref="IPublishStrategy"/>. Completes immediately if no handlers are registered.
    /// When polymorphic dispatch is enabled, handlers registered for base notification types and
    /// implemented notification interfaces are invoked as well.
    /// </summary>
    /// <param name="notification">The notification instance to publish.</param>
    /// <param name="cancellationToken">Cancellation token propagated to every handler invocation.</param>
    /// <typeparam name="TNotification">The notification type used to resolve handlers.</typeparam>
    /// <returns>A task that completes when the configured strategy has finished running all handlers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification"/> is <see langword="null"/>.</exception>
    public Task PublishAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default
    ) where TNotification : INotification
    {
        if (notification is null) throw new ArgumentNullException(nameof(notification));

        // IMPORTANT: resolve from the ambient provider (which, in ASP.NET Core / generic host,
        // is the current *scope* when this class itself is registered as scoped). This keeps
        // handlers in the same DI scope as the caller (e.g. same DbContext instance), avoiding
        // the classic "new scope per handler" footgun.
        var calls = enablePolymorphicDispatch
            ? BuildPolymorphicCalls(notification)
            : BuildExactCalls(notification);

        return calls.Count == 0
            ? Task.CompletedTask
            : _strategy.PublishAsync(calls, cancellationToken);
    }

    private List<Func<CancellationToken, Task>> BuildExactCalls<TNotification>(TNotification notification)
        where TNotification : INotification
    {
        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();

        var calls = new List<Func<CancellationToken, Task>>();
        foreach (var handler in handlers)
        {
            calls.Add(token => handler.HandleAsync(notification, token));
        }

        return calls;
    }

    private List<Func<CancellationToken, Task>> BuildPolymorphicCalls(INotification notification)
    {
        var handlerInterfaces = PolymorphicHandlerCache.GetOrAdd(notification.GetType(), BuildHandlerInterfaces);

        var calls = new List<Func<CancellationToken, Task>>();
        foreach (var (handlerInterface, handleAsync) in handlerInterfaces)
        {
            foreach (var handler in _serviceProvider.GetServices(handlerInterface).Where(h => h is not null))
            {
                calls.Add(token => (Task) handleAsync.Invoke(handler, [notification, token])!);
            }
        }

        return calls;
    }

    private static (Type HandlerInterface, MethodInfo HandleAsync)[] BuildHandlerInterfaces(Type notificationType)
    {
        var notificationTypes = new List<Type>();

        // The runtime type itself and every base class that is a notification.
        for (var current = notificationType; current is not null; current = current.BaseType)
        {
            if (typeof(INotification).IsAssignableFrom(current))
            {
                notificationTypes.Add(current);
            }
        }

        // Every implemented interface that is (or derives from) INotification.
        notificationTypes.AddRange(
            notificationType.GetInterfaces()
                .Where(i => i != typeof(INotification) && typeof(INotification).IsAssignableFrom(i))
        );

        return
        [
            .. notificationTypes
                .Select(t =>
                {
                    var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(t);
                    var handleAsync =
                        handlerInterface.GetMethod(nameof(INotificationHandler<>.HandleAsync))!;
                    return (handlerInterface, handleAsync);
                })
        ];
    }
}