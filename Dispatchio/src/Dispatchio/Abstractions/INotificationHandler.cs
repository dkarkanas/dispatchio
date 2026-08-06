namespace Dispatchio.Abstractions;

/// <summary>
/// Handles a specific <see cref="INotification"/> type. Register implementations with your DI
/// container (or use the assembly-scanning extensions in the EventBus package). Multiple handlers
/// may exist for the same notification type; all of them will be invoked on publish.
/// </summary>
/// <typeparam name="TNotification">The notification type this handler responds to.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the given notification.
    /// </summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">Cancellation token propagated from the publish call.</param>
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}
