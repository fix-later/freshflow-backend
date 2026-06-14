using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Pricing.Application.EventHandlers;

/// <summary>
/// Handles <see cref="PriceUpdatedDomainEvent"/> by broadcasting the committed price update
/// to SignalR clients via <see cref="IPricingBroadcastService"/>.
///
/// Dispatch contract:
///   - This handler runs post-commit (<see cref="FreshFlow.Infrastructure.Persistence.AppDbContext"/>
///     dispatches events only after <c>base.SaveChangesAsync</c> succeeds) so clients never
///     observe a price that was later rolled back.
///   - Broadcast failures are caught and logged; they must not fail the originating
///     price-update request or roll back the already-committed DB write.
/// </summary>
internal sealed class PriceUpdatedDomainEventHandler(
    IPricingBroadcastService broadcastService,
    ILogger<PriceUpdatedDomainEventHandler> logger)
    : INotificationHandler<PriceUpdatedDomainEvent>
{
    public async Task Handle(
        PriceUpdatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = new PriceUpdateBroadcastDto(
                notification.MarketProductId,
                notification.MarketId,
                notification.ProductId,
                notification.OldPrice,
                notification.NewPrice,
                notification.CurrentQuantity,
                notification.UpdatedBy,
                notification.OccurredAt);

            await broadcastService.BroadcastPriceUpdateAsync(dto, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancellation: always propagate so the caller can honour the token.
            throw;
        }
        catch (Exception ex)
        {
            // Intentionally swallowed — broadcast failure must not fail the price update.
            logger.LogError(
                ex,
                "Failed to broadcast price update for MarketId={MarketId} ProductId={ProductId}",
                notification.MarketId,
                notification.ProductId);
        }
    }
}
