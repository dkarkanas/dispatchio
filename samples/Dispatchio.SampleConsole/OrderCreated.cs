using Dispatchio.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dispatchio.SampleConsole;

public sealed record OrderCreated(Guid OrderId, decimal Total) : INotification;

public sealed class SendOrderConfirmationEmailHandler(
    ILogger<SendOrderConfirmationEmailHandler> logger
) : INotificationHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Emailing confirmation for order {OrderId} (${Total})",
            notification.OrderId, notification.Total);

        return Task.CompletedTask;
    }
}

public sealed class UpdateInventoryHandler(
    ILogger<UpdateInventoryHandler> logger
) : INotificationHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Reserving inventory for order {OrderId}", notification.OrderId);
        
        return Task.CompletedTask;
    }
}