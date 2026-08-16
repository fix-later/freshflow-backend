namespace FreshFlow.Hub.Application.Abstractions;

public interface IOrderLookupReader
{
    public Task<OrderLookupDto?> FindByOrderItemIdAsync(Guid orderItemId, CancellationToken ct);

    public Task<IReadOnlyList<OrderLookupDto>> FindByOrderItemIdsAsync(
        IReadOnlyCollection<Guid> orderItemIds,
        CancellationToken ct);
}

public sealed record OrderLookupDto(
    Guid OrderItemId,
    Guid OrderId,
    Guid MarketProductId,
    decimal Quantity,
    decimal? ActualQuantity);
