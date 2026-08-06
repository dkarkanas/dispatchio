namespace Dispatchio.Abstractions;

/// <summary>
/// Publishes notifications to all registered handlers for that notification type.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a notification to every registered <see cref="INotificationHandler{TNotification}"/>
    /// for <typeparamref name="TNotification"/>. Behavior when multiple handlers exist (parallel vs
    /// sequential, exception aggregation) is determined by the configured <c>IPublishStrategy</c>.
    /// </summary>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
