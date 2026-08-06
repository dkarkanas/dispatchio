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
    cfg.RegisterHandlersFromAssemblyContaining<OrderCreated>();
    
    // Both confirmation email and inventory update are independent — run them concurrently.
    cfg.UseStrategy<WhenAllPublishStrategy>();
});

using var host = builder.Build();

var publisher = host.Services.GetRequiredService<IEventPublisher>();
await publisher.PublishAsync(new OrderCreated(Guid.NewGuid(), 129.99m));
