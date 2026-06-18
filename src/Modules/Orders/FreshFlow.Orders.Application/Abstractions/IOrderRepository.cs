using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IOrderRepository
{
    public Task<Order?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<IReadOnlyList<Order>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct);

    public Task<(IReadOnlyList<Order> Orders, int Total)> SearchAsync(
        OrderSearchCriteria criteria, CancellationToken ct);

    public Task AddAsync(Order order, CancellationToken ct);

    public void Track(Order order);

    public void TrackNewItem(OrderItem item);

    public Task SaveChangesAsync(CancellationToken ct);
}

public sealed record OrderSearchCriteria(
    Guid? RestaurantId,
    OrderStatus? Status,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    bool SortAscending,
    int Page,
    int PageSize);
