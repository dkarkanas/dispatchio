namespace Dispatchio.Abstractions;

/// <summary>
/// Marker interface for in-process notifications (events) that can have zero or more handlers.
/// Implement this on any event/DTO you want to publish through <see cref="IEventPublisher"/>.
/// </summary>
public interface INotification;
