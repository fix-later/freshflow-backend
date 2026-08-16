using FreshFlow.Contracts;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Pricing.Application.EventHandlers;

internal sealed class ProcurementPurchaseConfirmedIntegrationEventHandler(
    IMarketProductRepository marketProducts,
    IPriceSnapshotRepository snapshots,
    ILogger<ProcurementPurchaseConfirmedIntegrationEventHandler> logger)
    : INotificationHandler<ProcurementPurchaseConfirmedIntegrationEvent>
{
    public async Task Handle(
        ProcurementPurchaseConfirmedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var (marketProductId, actualUnitPrice) in notification.ActualUnitPrices)
        {
            var marketProduct = await marketProducts.FindByIdAsync(marketProductId, cancellationToken);
            if (marketProduct is null || marketProduct.MarketId != notification.MarketId)
            {
                logger.LogWarning(
                    "Skipping purchase price sync for MarketProductId={MarketProductId}, BatchId={BatchId}.",
                    marketProductId,
                    notification.BatchId);
                continue;
            }

            marketProducts.Track(marketProduct);
            marketProduct.UpdatePrice(actualUnitPrice, notification.AgentUserId);
            await snapshots.AddAsync(
                PriceSnapshot.For(marketProduct, notification.AgentUserId),
                cancellationToken);
            changed = true;
        }

        if (changed)
            await marketProducts.SaveChangesAsync(cancellationToken);
    }
}
