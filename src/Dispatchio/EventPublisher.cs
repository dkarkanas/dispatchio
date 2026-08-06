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
    IPublishStrategy strategy
) : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private readonly IPublishStrategy _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    /// <summary>
    /// Publishes the notification to every registered
    /// <see cref="INotificationHandler{TNotification}"/>, executing them according to the
    /// configured <see cref="IPublishStrategy"/>. Completes immediately if no handlers are registered.
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
        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();

        var calls = new List<Func<CancellationToken, Task>>();
        foreach (var handler in handlers)
        {
            calls.Add(token => handler.HandleAsync(notification, token));
        }

        return calls.Count == 0 
            ? Task.CompletedTask 
            : _strategy.PublishAsync(calls, cancellationToken);
    }
}