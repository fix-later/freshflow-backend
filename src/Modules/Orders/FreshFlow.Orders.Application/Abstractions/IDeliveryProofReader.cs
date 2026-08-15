namespace FreshFlow.Orders.Application.Abstractions;

public interface IDeliveryProofReader
{
    public Task<string?> FindByOrderIdAsync(Guid orderId, CancellationToken ct);
}
