using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IScheduledOrderRepository
{
    public Task<ScheduledOrder?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<IReadOnlyList<ScheduledOrder>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct);

    public Task<IReadOnlyList<ScheduledOrder>> GetActiveAsync(CancellationToken ct);

    public Task AddAsync(ScheduledOrder scheduledOrder, CancellationToken ct);

    public void Track(ScheduledOrder scheduledOrder);

    public Task SaveChangesAsync(CancellationToken ct);
}
