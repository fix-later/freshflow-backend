using System.Text.Json;
using FreshFlow.Contracts;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Infrastructure.Persistence.Audit;

internal sealed class PriceUpdatedAuditLogHandler(IAuditLogWriter writer)
    : INotificationHandler<PriceUpdatedIntegrationEvent>
{
    public Task Handle(PriceUpdatedIntegrationEvent notification, CancellationToken cancellationToken) =>
        writer.WriteAsync(
            actorId: notification.UpdatedBy,
            action: "price_updated",
            entityType: "market_product",
            entityId: notification.MarketProductId,
            details: JsonSerializer.Serialize(new
            {
                marketId = notification.MarketId,
                productId = notification.ProductId,
                oldPrice = notification.OldPrice,
                newPrice = notification.NewPrice,
                currentQuantity = notification.CurrentQuantity,
            }),
            occurredAt: notification.OccurredAt,
            ct: cancellationToken);
}
