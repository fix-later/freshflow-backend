using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IOrderRepository
{
    public Task<Order?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<IReadOnlyList<Order>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct);

    public Task AddAsync(Order order, CancellationToken ct);

    public void Track(Order order);

    public void TrackNewItem(OrderItem item);

    public Task SaveChangesAsync(CancellationToken ct);
}
