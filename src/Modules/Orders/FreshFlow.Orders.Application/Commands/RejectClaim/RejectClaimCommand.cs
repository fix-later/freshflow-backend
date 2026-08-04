using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.RejectClaim;

public sealed record RejectClaimCommand(
    Guid ReviewedBy,
    Guid ClaimId,
    string DecisionNote) : ICommand<OrderClaimDto>;
