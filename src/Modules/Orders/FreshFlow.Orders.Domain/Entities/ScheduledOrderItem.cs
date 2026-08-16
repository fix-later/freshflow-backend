using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

/// <summary>
/// One line of a <see cref="ScheduledOrder"/>'s item template. Only id + quantity are stored —
/// price is never persisted here, always snapshotted live from <c>market_products</c> when the
/// background job generates a concrete <see cref="Order"/> from this template.
/// </summary>
public sealed class ScheduledOrderItem : BaseEntity
{
    private ScheduledOrderItem() { } // EF Core

    public ScheduledOrderItem(Guid marketProductId, int quantity)
    {
        ValidateQuantity(quantity);

        MarketProductId = marketProductId;
        Quantity = quantity;
    }

    public Guid ScheduledOrderId { get; private set; }
    public Guid MarketProductId { get; private set; }
    public int Quantity { get; private set; }

    public void UpdateQuantity(int quantity)
    {
        ValidateQuantity(quantity);
        Quantity = quantity;
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
    }
}
