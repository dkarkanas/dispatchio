# Dispatchio
<img src="icon/logo.svg" alt="Dispatchio logo" width="200" />

A lightweight in-process notification dispatcher for .NET — a drop-in replacement for MediatR's
`INotification` / `IPublisher` pipeline, with pluggable publish strategies and DI-based assembly
scanning. No request/response ("send") pipeline is included by design — this library covers
fire-and-forget notifications only.

> ⚠️ **Status:** Dispatchio is in beta and has **not been battle-tested in production**. It is
> intended as a MediatR replacement for small projects or workloads without heavy business
> requirements. Use it at your own discretion, test thoroughly, and please report any issues.

## Install

```bash
dotnet add package Dispatchio
```

## Quickstart

```csharp
using Dispatchio;
using Dispatchio.Abstractions;
using Dispatchio.Strategies;

// 1. Define a notification
public sealed record OrderCreated(Guid OrderId, decimal Total) : INotification;

// 2. Write one or more handlers
public sealed class SendConfirmationEmail : INotificationHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated notification, CancellationToken cancellationToken = default)
    {
        // ...
        return Task.CompletedTask;
    }
}

// 3. Register (Program.cs)
builder.Services.AddDispatchio(cfg =>
{
    cfg.RegisterHandlersFromAssemblyContaining<OrderCreated>();
    cfg.UseStrategy<WhenAllPublishStrategy>(); // optional, defaults to sequential
});

// 4. Publish (inject IEventPublisher)
await eventPublisher.PublishAsync(new OrderCreated(orderId, total));
```

## Publish strategies

| Strategy | Behavior |
|---|---|
| `ForeachAwaitPublishStrategy` (default) | Sequential, in registration order. First exception stops remaining handlers and propagates immediately. Matches MediatR's default `Publish` behavior. |
| `WhenAllPublishStrategy` | All handlers run concurrently. If multiple handlers fail, all their exceptions are surfaced together via `AggregateException`. |

Write your own by implementing `IPublishStrategy` and passing it to `UseStrategy<T>()`.

## Migrating from MediatR

Dispatchio mirrors MediatR's notification-side naming intentionally, so migration is close to a
find-and-replace for the notification/handler surface:

| MediatR | Dispatchio |
|---|---|
| `MediatR.INotification` | `Dispatchio.Abstractions.INotification` |
| `MediatR.INotificationHandler<T>` | `Dispatchio.Abstractions.INotificationHandler<T>` (method is `HandleAsync`, not `Handle`, and always async) |
| `IPublisher.Publish(x)` / `IMediator.Publish(x)` | `IEventPublisher.PublishAsync(x)` |
| `services.AddMediatR(...)` | `services.AddDispatchio(cfg => cfg.RegisterHandlersFromAssemblyContaining<T>())` |

Steps:
1. `dotnet add package Dispatchio` to each project that references MediatR notifications.
2. Replace `using MediatR;` with `using Dispatchio.Abstractions;`.
3. Rename handler methods from `Handle` to `HandleAsync` (a small regex/find-replace across the
   solution handles most of this).
4. Swap `services.AddMediatR(...)` for `services.AddDispatchio(...)`.
5. Replace injected `IPublisher`/`IMediator` usages with `IEventPublisher`, and `.Publish(x)` with
   `.PublishAsync(x)`.
6. Remove the MediatR package reference once nothing else in the project uses its request/response
   (`IRequest`/`IRequestHandler`) pipeline. If you still use that elsewhere, keep MediatR installed
   alongside this package — they don't conflict.

## Project layout

```
src/Dispatchio/                     → EventPublisher, built-in strategies, DI registration extensions
src/Dispatchio/Abstractions/        → INotification, INotificationHandler<T>, IEventPublisher, IPublishStrategy
test/UnitTests/                     → xUnit unit tests
benchmark/Dispatchio.Benchmark/     → BenchmarkDotNet comparison vs MediatR
samples/Dispatchio.SampleConsole/   → minimal console host wiring everything up
```

## Design notes

- `IEventPublisher` is registered **scoped**, so handlers resolve from the same DI scope as the
  caller (e.g. the same `DbContext` instance in an ASP.NET Core request) rather than a fresh scope.
- Handler discovery uses reflection over `Assembly.GetTypes()` at startup only (registration time),
  not per-publish-call, so runtime dispatch cost is just an `IServiceProvider.GetServices<T>()` call.
- Reflection-based scanning has not been validated under trimming/Native AOT. If that matters to
  you, consider a source-generator-based alternative for handler discovery.

## Building & testing locally

Clone the repo and run:

```bash
git clone https://github.com/dkarkanas/dispatchio.git
cd dispatchio
dotnet restore
dotnet build -c Release
dotnet test
dotnet run -c Release --project benchmark/Dispatchio.Benchmark
```

## Contributing

Contributions, bug reports, and forks are welcome! Feel free to open an issue or submit a pull
request on [GitHub](https://github.com/dkarkanas/dispatchio).

## License

Dispatchio is licensed under the [MIT License](LICENSE).
