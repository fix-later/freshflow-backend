namespace FreshFlow.Logistics.Application.Abstractions;

public interface IRestaurantOwnerReader
{
    public Task<Guid?> FindRestaurantIdByUserIdAsync(Guid userId, CancellationToken ct);
}
