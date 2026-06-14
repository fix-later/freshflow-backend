using FreshFlow.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core interceptor that dispatches domain events from tracked aggregates
/// AFTER a successful DB commit (<see cref="SavedChangesAsync"/>).
///
/// Sequence on each successful save:
///   1. Commit to DB (<c>base.SaveChangesAsync</c> has already run before this hook fires).
///   2. Collect domain events from all tracked <see cref="AggregateRoot"/> instances.
///   3. Clear events from aggregates (prevents double-dispatch on retry).
///   4. Dispatch each event via <see cref="IPublisher.Publish"/>.
///
/// Design guarantees:
///   - Events dispatched post-commit — downstream handlers (SignalR, Redis) observe
///     only persisted data; a broadcast failure CANNOT roll back the DB write.
///   - If DB save fails, <see cref="SavedChangesAsync"/> never fires so no events
///     are dispatched for uncommitted data.
///   - Post-commit dispatch errors are logged but NOT re-thrown. The DB write already
///     succeeded; propagating here would make callers believe the save failed and retry,
///     risking duplicate writes. Full outbox pattern is out of scope for v1.
///   - Interceptor is scoped (one instance per request) — each handler in the dispatch
///     chain correctly resolves its own scoped dependencies (e.g., <c>IPricingBroadcastService</c>).
/// </summary>
internal sealed class DomainEventDispatchInterceptor(
    IPublisher publisher,
    ILogger<DomainEventDispatchInterceptor> logger)
    : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is not null)
        {
            var aggregates = context.ChangeTracker
                .Entries<AggregateRoot>()
                .Select(e => e.Entity)
                .ToList();

            // Collect then clear — prevents double-dispatch if SaveChangesAsync is retried.
            var domainEvents = aggregates
                .SelectMany(a => a.DomainEvents)
                .ToList();

            foreach (var aggregate in aggregates)
                aggregate.ClearDomainEvents();

            // Dispatch post-commit — each event is wrapped individually so a single handler
            // failure does not prevent the remaining events from being dispatched.
            foreach (var evt in domainEvents)
            {
                try
                {
                    await publisher.Publish(evt, cancellationToken);
                }
                catch (OperationCanceledException oce)
                {
                    // Cancellation after a committed write: log and stop dispatching
                    // remaining events for this save, but do NOT re-throw — the caller
                    // would interpret it as a save failure even though the DB committed.
                    logger.LogWarning(
                        oce,
                        "Post-commit event dispatch cancelled for {EventType}. DB write succeeded.",
                        evt.GetType().Name);
                    break;
                }
                catch (Exception ex)
                {
                    // Non-cancellation failure: log at Error but continue to next event.
                    // Failing here would incorrectly signal to the caller that SaveChangesAsync
                    // failed, potentially causing a retry that double-writes data.
                    logger.LogError(
                        ex,
                        "Post-commit event dispatch failed for {EventType}. DB write succeeded.",
                        evt.GetType().Name);
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
