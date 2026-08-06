using Dispatchio;
using Dispatchio.Abstractions;
using Dispatchio.Strategies;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UnitTests.TestFixtures;

namespace UnitTests;

public class PolymorphicDispatchTests
{
    private static IServiceProvider BuildProvider(
        UserRegisteredHandler concrete,
        BaseUserNotificationHandler baseHandler,
        AuditHandler audit)
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<UserRegisteredNotification>>(concrete);
        services.AddSingleton<INotificationHandler<BaseUserNotification>>(baseHandler);
        services.AddSingleton<INotificationHandler<IAuditableNotification>>(audit);
        services.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PublishAsync_WithPolymorphicDispatchDisabled_OnlyInvokesExactTypeHandlers()
    {
        var concrete = new UserRegisteredHandler();
        var baseHandler = new BaseUserNotificationHandler();
        var audit = new AuditHandler();
        var provider = BuildProvider(concrete, baseHandler, audit);
        var publisher = new EventPublisher(provider, provider.GetRequiredService<IPublishStrategy>());

        await publisher.PublishAsync(new UserRegisteredNotification("alice"));

        concrete.Received.Should().ContainSingle().Which.Should().Be("alice");
        baseHandler.Received.Should().BeEmpty("base-class handlers must not run when polymorphic dispatch is disabled");
        audit.Received.Should().BeEmpty("interface handlers must not run when polymorphic dispatch is disabled");
    }

    [Fact]
    public async Task PublishAsync_WithPolymorphicDispatchEnabled_InvokesBaseClassAndInterfaceHandlers()
    {
        var concrete = new UserRegisteredHandler();
        var baseHandler = new BaseUserNotificationHandler();
        var audit = new AuditHandler();
        var provider = BuildProvider(concrete, baseHandler, audit);
        var publisher = new EventPublisher(
            provider, provider.GetRequiredService<IPublishStrategy>(), enablePolymorphicDispatch: true);

        await publisher.PublishAsync(new UserRegisteredNotification("bob"));

        concrete.Received.Should().ContainSingle().Which.Should().Be("bob");
        baseHandler.Received.Should().ContainSingle().Which.Should().Be("bob");
        audit.Received.Should().ContainSingle().Which.Should().Be(typeof(UserRegisteredNotification));
    }

    [Fact]
    public async Task PublishAsync_WithPolymorphicDispatchEnabled_UsesRuntimeTypeNotCompileTimeType()
    {
        var concrete = new UserRegisteredHandler();
        var baseHandler = new BaseUserNotificationHandler();
        var audit = new AuditHandler();
        var provider = BuildProvider(concrete, baseHandler, audit);
        var publisher = new EventPublisher(
            provider, provider.GetRequiredService<IPublishStrategy>(), enablePolymorphicDispatch: true);

        // Publish through a base-typed variable: dispatch must still be based on the *runtime* type.
        BaseUserNotification notification = new UserRegisteredNotification("carol");
        await publisher.PublishAsync(notification);

        concrete.Received.Should().ContainSingle().Which.Should().Be("carol");
        baseHandler.Received.Should().ContainSingle().Which.Should().Be("carol");
        audit.Received.Should().ContainSingle().Which.Should().Be(typeof(UserRegisteredNotification));
    }

    [Fact]
    public async Task PublishAsync_WithPolymorphicDispatchEnabled_AndBaseNotification_DoesNotInvokeDerivedHandlers()
    {
        var concrete = new UserRegisteredHandler();
        var baseHandler = new BaseUserNotificationHandler();
        var audit = new AuditHandler();
        var provider = BuildProvider(concrete, baseHandler, audit);
        var publisher = new EventPublisher(
            provider, provider.GetRequiredService<IPublishStrategy>(), enablePolymorphicDispatch: true);

        // A plain base notification: only the base handler applies. It is not IAuditableNotification
        // and it is not a UserRegisteredNotification.
        await publisher.PublishAsync(new BaseUserNotification("dave"));

        baseHandler.Received.Should().ContainSingle().Which.Should().Be("dave");
        concrete.Received.Should().BeEmpty();
        audit.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WithPolymorphicDispatchEnabled_AndNoMatchingHandlers_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPublishStrategy, ForeachAwaitPublishStrategy>();
        var provider = services.BuildServiceProvider();
        var publisher = new EventPublisher(
            provider, provider.GetRequiredService<IPublishStrategy>(), enablePolymorphicDispatch: true);

        var act = () => publisher.PublishAsync(new UserRegisteredNotification("nobody"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddDispatchio_EnablePolymorphicDispatch_WiresUpPolymorphicPublisher()
    {
        var services = new ServiceCollection();
        services.AddDispatchio(cfg =>
        {
            cfg.RegisterHandlersFromAssemblyContaining<UserRegisteredHandler>();
            cfg.EnablePolymorphicDispatch();
            cfg.WithHandlerLifetime(ServiceLifetime.Singleton);
        });
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new UserRegisteredNotification("eve"));

        var baseHandler = (BaseUserNotificationHandler)provider
            .GetRequiredService<INotificationHandler<BaseUserNotification>>();
        var audit = (AuditHandler)provider
            .GetRequiredService<INotificationHandler<IAuditableNotification>>();

        baseHandler.Received.Should().ContainSingle().Which.Should().Be("eve");
        audit.Received.Should().ContainSingle().Which.Should().Be(typeof(UserRegisteredNotification));
    }

    [Fact]
    public async Task AddDispatchio_WithoutEnablePolymorphicDispatch_KeepsExactTypeDispatch()
    {
        var services = new ServiceCollection();
        services.AddDispatchio(cfg =>
        {
            cfg.RegisterHandlersFromAssemblyContaining<UserRegisteredHandler>();
            cfg.WithHandlerLifetime(ServiceLifetime.Singleton);
        });
        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new UserRegisteredNotification("frank"));

        var baseHandler = (BaseUserNotificationHandler)provider
            .GetRequiredService<INotificationHandler<BaseUserNotification>>();

        baseHandler.Received.Should().BeEmpty("polymorphic dispatch is disabled by default");
    }
}

