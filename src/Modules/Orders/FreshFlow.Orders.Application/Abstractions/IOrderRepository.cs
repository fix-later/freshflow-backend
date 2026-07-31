using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IOrderRepository
{
    public Task<Result> ExecuteInSerializableTransactionAsync(
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken ct);

    public Task<bool> TryReserveStockAsync(IReadOnlyList<StockReservation> reservations, CancellationToken ct);

    public Task<bool> ReleaseStockAsync(IReadOnlyList<StockReservation> reservations, CancellationToken ct);

    public Task<bool> ConsumeStockAsync(IReadOnlyList<StockReservation> reservations, CancellationToken ct);

    public Task<Order?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<IReadOnlyList<Order>> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken ct);

    public Task<(IReadOnlyList<Order> Orders, int Total)> SearchAsync(
        OrderSearchCriteria criteria, CancellationToken ct);

    public Task<(IReadOnlyList<Order> Orders, int Total)> GetByScheduledOrderIdAsync(
        Guid scheduledOrderId, int page, int pageSize, CancellationToken ct);

    public Task AddAsync(Order order, CancellationToken ct);

    public void Track(Order order);

    public void TrackNewItem(OrderItem item);

    public Task SaveChangesAsync(CancellationToken ct);
}

public sealed record StockReservation(Guid MarketProductId, int Quantity);

public sealed record OrderSearchCriteria(
    Guid? RestaurantId,
    OrderStatus? Status,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    bool SortAscending,
    int Page,
    int PageSize);
