using FreshFlow.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
///   - Interceptor is scoped (one instance per request) — each handler in the dispatch
///     chain correctly resolves its own scoped dependencies (e.g., <c>IPricingBroadcastService</c>).
/// </summary>
internal sealed class DomainEventDispatchInterceptor(IPublisher publisher)
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

            // Dispatch post-commit — each handler is responsible for its own error handling.
            foreach (var evt in domainEvents)
                await publisher.Publish(evt, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
