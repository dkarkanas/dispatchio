using Dispatchio;
using Dispatchio.Abstractions;
using Dispatchio.SampleConsole;
using Dispatchio.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

builder.Services.AddDispatchio(cfg =>
{
    cfg.RegisterHandlersFromAssembly(typeof(OrderCreated).Assembly);
    
    // Both confirmation email and inventory update are independent — run them concurrently.
    cfg.UseStrategy<WhenAllPublishStrategy>();

    // Also dispatch to handlers of base notification types and notification interfaces
    // (see the UserRegistered scenario below).
    cfg.EnablePolymorphicDispatch();
});

using var host = builder.Build();

var publisher = host.Services.GetRequiredService<IEventPublisher>();
await publisher.PublishAsync(new OrderCreated(Guid.NewGuid(), 129.99m));

// Polymorphic dispatch scenario: a single UserRegistered event fans out to the concrete handler
// (WelcomeEmailHandler), the base-class handler (UserEventMetricsHandler), and the interface
// handler (AuditTrailHandler) — because EnablePolymorphicDispatch() was configured above.
await publisher.PublishAsync(new UserRegistered(Guid.NewGuid(), "jane.doe@example.com"));

