using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IOrderClaimRepository
{
    public Task<OrderClaim?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<(IReadOnlyList<OrderClaim> Claims, string? NextCursor)> SearchAsync(
        OrderClaimSearchCriteria criteria,
        CancellationToken ct);

    public Task AddAsync(OrderClaim claim, CancellationToken ct);

    public void Track(OrderClaim claim);

    public Task SaveChangesAsync(CancellationToken ct);
}

public sealed record OrderClaimSearchCriteria(
    Guid? RestaurantId,
    OrderClaimStatus? Status,
    string? Cursor,
    int PageSize);
