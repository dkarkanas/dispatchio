namespace Dispatchio.Abstractions;

/// <summary>
/// Determines how a set of resolved handler invocations for a single publish call are executed
/// (e.g. sequential vs parallel) and how exceptions from multiple handlers are surfaced.
/// Implement this to plug in custom dispatch behavior.
/// </summary>
public interface IPublishStrategy
{
    /// <summary>
    /// Executes the given handler invocations according to this strategy's policy.
    /// </summary>
    /// <param name="handlerCalls">
    /// One callback per resolved handler. Each callback, when invoked, runs that handler.
    /// </param>
    /// <param name="cancellationToken">Cancellation token propagated from the publish call.</param>
    Task PublishAsync(
        IReadOnlyList<Func<CancellationToken, Task>> handlerCalls,
        CancellationToken cancellationToken
    );
}
