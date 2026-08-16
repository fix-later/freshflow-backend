using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetClaimById;

public sealed record GetClaimByIdQuery(
    Guid UserId,
    bool IsPrivileged,
    Guid ClaimId) : IQuery<OrderClaimDto>;
