using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.ApproveClaim;

public sealed record ApproveClaimCommand(
    Guid ReviewedBy,
    Guid ClaimId,
    string? DecisionNote) : ICommand<OrderClaimDto>;
