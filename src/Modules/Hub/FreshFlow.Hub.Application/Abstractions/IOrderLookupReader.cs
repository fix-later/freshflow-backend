namespace FreshFlow.Hub.Application.Abstractions;

public interface IOrderLookupReader
{
    public Task<OrderLookupDto?> FindByOrderItemIdAsync(Guid orderItemId, CancellationToken ct);
}

public sealed record OrderLookupDto(Guid OrderItemId, Guid OrderId);
