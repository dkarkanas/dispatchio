using Dispatchio.Abstractions;

namespace Dispatchio.Strategies;

/// <summary>
/// Runs all handlers concurrently via <see cref="Task.WhenAll(IEnumerable{Task})"/>. If more than
/// one handler throws, the resulting <see cref="AggregateException"/> contains all of them
/// (unlike raw <c>Task.WhenAll</c>, which only surfaces the first). Use this when handlers are
/// independent and you want one slow/failing handler to not block the others.
/// </summary>
public sealed class WhenAllPublishStrategy : IPublishStrategy
{
    /// <summary>
    /// Starts all handler invocations immediately and awaits their combined completion via
    /// <see cref="Task.WhenAll(Task[])"/>.
    /// </summary>
    /// <param name="handlerCalls">One callback per resolved handler. Each callback, when invoked, runs that handler.</param>
    /// <param name="cancellationToken">Cancellation token propagated to every handler invocation.</param>
    /// <returns>A task that completes when all handler invocations have finished.</returns>
    /// <exception cref="AggregateException">
    /// Thrown when two or more handlers fail; contains the exceptions from every faulted handler.
    /// If exactly one handler fails, its original exception propagates unchanged.
    /// </exception>
    public async Task PublishAsync(
        IReadOnlyList<Func<CancellationToken, Task>> handlerCalls,
        CancellationToken cancellationToken
    )
    {
        var tasks = new Task[handlerCalls.Count];
        for (var i = 0; i < handlerCalls.Count; i++)
        {
            tasks[i] = handlerCalls[i](cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            var exceptions = tasks
                .Where(t => t is {IsFaulted: true, Exception: not null})
                .SelectMany(t => t.Exception!.InnerExceptions)
                .ToList();

            if (exceptions.Count > 1)
            {
                throw new AggregateException(
                    $"{exceptions.Count} notification handlers failed.", exceptions);
            }

            throw;
        }
    }
}
