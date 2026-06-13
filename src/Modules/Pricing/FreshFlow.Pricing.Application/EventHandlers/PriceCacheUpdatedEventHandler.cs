using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Pricing.Application.EventHandlers;

/// <summary>
/// Handles <see cref="PriceUpdatedDomainEvent"/> by writing the new price snapshot
/// to the Redis price board (UC-PRI-07).
///
/// Dispatch contract:
///   - This handler runs post-commit (via <see cref="FreshFlow.Infrastructure.Persistence.DomainEventDispatchInterceptor"/>)
///     so the cache always reflects persisted data.
///   - Redis failures are caught and logged at Warning level — they must not fail
///     the originating price-update request or roll back the already-committed DB write.
///   - The <c>reserved</c> field is intentionally NOT written here; it is owned by
///     the Orders module.
/// </summary>
internal sealed class PriceCacheUpdatedEventHandler(
    IPriceBoardCacheWriter cacheWriter,
    ILogger<PriceCacheUpdatedEventHandler> logger)
    : INotificationHandler<PriceUpdatedDomainEvent>
{
    public async Task Handle(
        PriceUpdatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await cacheWriter.WriteAsync(
                marketId: notification.MarketId,
                productId: notification.ProductId,
                price: notification.NewPrice,
                quantity: notification.CurrentQuantity,
                updatedAt: notification.OccurredAt,
                updatedBy: notification.UpdatedBy,
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            // Intentionally swallowed — Redis failure must not fail the price update.
            // Log at Warning: Redis unavailability is expected under maintenance/failover.
            logger.LogWarning(
                ex,
                "Failed to update Redis price board for MarketId={MarketId} ProductId={ProductId}",
                notification.MarketId,
                notification.ProductId);
        }
    }
}
