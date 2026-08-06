using Dispatchio.Abstractions;

namespace Dispatchio.Strategies;

/// <summary>
/// Runs handlers one at a time, in registration order, awaiting each before starting the next.
/// The first handler to throw stops the remaining handlers from running and the exception
/// propagates immediately (this mirrors MediatR's default <c>Publish</c> behavior). Choose
/// <see cref="WhenAllPublishStrategy"/> instead if handler independence matters more than
/// deterministic ordering.
/// </summary>
public sealed class ForeachAwaitPublishStrategy : IPublishStrategy
{
    /// <summary>
    /// Invokes each handler sequentially in registration order, awaiting each one before starting
    /// the next. The first exception stops execution and propagates to the caller.
    /// </summary>
    /// <param name="handlerCalls">One callback per resolved handler. Each callback, when invoked, runs that handler.</param>
    /// <param name="cancellationToken">Cancellation token checked before each handler invocation and propagated to it.</param>
    /// <returns>A task that completes when all handler invocations have finished.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled before the next handler starts.
    /// </exception>
    public async Task PublishAsync(
        IReadOnlyList<Func<CancellationToken, Task>> handlerCalls,
        CancellationToken cancellationToken
    )
    {
        foreach (var call in handlerCalls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await call(cancellationToken).ConfigureAwait(false);
        }
    }
}