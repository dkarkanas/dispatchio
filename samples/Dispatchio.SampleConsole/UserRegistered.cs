using Dispatchio.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dispatchio.SampleConsole;

// --- Polymorphic dispatch scenario ------------------------------------------------------------
// UserRegistered derives from UserEvent and implements IAuditableEvent. With
// cfg.EnablePolymorphicDispatch() a single PublishAsync(new UserRegistered(...)) fans out to:
//   1. WelcomeEmailHandler        (handles the concrete UserRegistered type)
//   2. UserEventMetricsHandler    (handles the UserEvent base class)
//   3. AuditTrailHandler          (handles the IAuditableEvent interface)
// Without the option, only WelcomeEmailHandler would run.

/// <summary>Marker for events that must be written to the audit trail.</summary>
public interface IAuditableEvent : INotification
{
    string Description { get; }
}

/// <summary>Base class for all user-related events.</summary>
public abstract record UserEvent(Guid UserId) : INotification;

public sealed record UserRegistered(Guid UserId, string Email) : UserEvent(UserId), IAuditableEvent
{
    public string Description => $"User {UserId} registered with {Email}";
}

/// <summary>Handles the concrete event type — runs with or without polymorphic dispatch.</summary>
public sealed class WelcomeEmailHandler(
    ILogger<WelcomeEmailHandler> logger
) : INotificationHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Sending welcome email to {Email}", notification.Email);
        return Task.CompletedTask;
    }
}

/// <summary>Handles the base class — only runs when polymorphic dispatch is enabled.</summary>
public sealed class UserEventMetricsHandler(
    ILogger<UserEventMetricsHandler> logger
) : INotificationHandler<UserEvent>
{
    public Task HandleAsync(UserEvent notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Recording metric for user event {EventType} (user {UserId})",
            notification.GetType().Name, notification.UserId);
        return Task.CompletedTask;
    }
}

/// <summary>Handles the marker interface — only runs when polymorphic dispatch is enabled.</summary>
public sealed class AuditTrailHandler(
    ILogger<AuditTrailHandler> logger
) : INotificationHandler<IAuditableEvent>
{
    public Task HandleAsync(IAuditableEvent notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Audit trail: {Description}", notification.Description);
        return Task.CompletedTask;
    }
}

