using Dispatchio.Abstractions;

namespace UnitTests.TestFixtures;

public sealed record SampleNotification(string Payload) : INotification;

public sealed record OtherNotification(int Value) : INotification;

/// <summary>Records every notification it receives, in order, for assertions.</summary>
public sealed class RecordingHandler : INotificationHandler<SampleNotification>
{
    public List<string> Received { get; } = new();

    public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
    {
        Received.Add(notification.Payload);
        return Task.CompletedTask;
    }
}

/// <summary>A second handler for the same notification, to test multi-handler fan-out.</summary>
public sealed class SecondRecordingHandler : INotificationHandler<SampleNotification>
{
    public List<string> Received { get; } = new();

    public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
    {
        Received.Add(notification.Payload);
        return Task.CompletedTask;
    }
}

/// <summary>Always throws — used to test exception propagation policies.</summary>
public sealed class ThrowingHandler : INotificationHandler<SampleNotification>
{
    public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"Boom from {nameof(ThrowingHandler)}");
}

/// <summary>Delays then throws — used with WhenAll to verify multiple failures aggregate.</summary>
public sealed class DelayedThrowingHandler : INotificationHandler<SampleNotification>
{
    private readonly int _delayMs;
    public DelayedThrowingHandler(int delayMs = 10) => _delayMs = delayMs;

    public async Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delayMs, cancellationToken);
        throw new InvalidOperationException($"Boom from {nameof(DelayedThrowingHandler)}");
    }
}

/// <summary>Checks whether the CancellationToken it received was already cancelled.</summary>
public sealed class CancellationAwareHandler : INotificationHandler<SampleNotification>
{
    public bool ReceivedCancelledToken { get; private set; }

    public Task HandleAsync(SampleNotification notification, CancellationToken cancellationToken = default)
    {
        ReceivedCancelledToken = cancellationToken.IsCancellationRequested;
        return Task.CompletedTask;
    }
}
