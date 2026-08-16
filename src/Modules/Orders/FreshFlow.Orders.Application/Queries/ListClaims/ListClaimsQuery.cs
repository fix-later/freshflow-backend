using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.ListClaims;

public sealed record ListClaimsQuery(
    Guid UserId,
    bool IsPrivileged,
    Guid? RestaurantId,
    string? Status,
    string? Cursor,
    int PageSize = 50) : IQuery<OrderClaimPageDto>;
