# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).~~~~

## [1.0.0] - 2026-08-06

### Added

- Initial beta release of **Dispatchio**, a lightweight in-process notification dispatcher for .NET.
- Core abstractions:
  - `INotification` — marker interface for notifications/events.
  - `INotificationHandler<TNotification>` — handler contract for processing notifications.
  - `IEventPublisher` — entry point for publishing notifications to registered handlers.
  - `IPublishStrategy` — pluggable strategy contract controlling how handlers are invoked.
- Built-in publish strategies:
  - `ForeachAwaitPublishStrategy` — invokes handlers sequentially, awaiting each one.
  - `WhenAllPublishStrategy` — invokes handlers concurrently via `Task.WhenAll`.
- `EventBusOptions` for configuring dispatch behavior.
- Dependency injection integration via `IServiceCollection` extension methods with automatic handler registration.
- Multi-targeting support: `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`.
- BenchmarkDotNet benchmark suite.
- Sample console application demonstrating usage.
- Unit test suite covering the publisher, publish strategies, and DI registration.

[Unreleased]: https://github.com/dkarkanas/dispatchio/compare/v1.0.0-beta01...HEAD
[1.0.0-beta01]: https://github.com/dkarkanas/dispatchio/releases/tag/v1.0.0-beta01

