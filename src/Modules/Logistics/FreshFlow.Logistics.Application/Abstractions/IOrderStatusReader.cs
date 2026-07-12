namespace FreshFlow.Logistics.Application.Abstractions;

public interface IOrderStatusReader
{
    public Task<OrderStatusLookupDto?> FindByIdAsync(Guid orderId, CancellationToken ct);
}

public sealed record OrderStatusLookupDto(Guid OrderId, string Status, Guid RestaurantId);
