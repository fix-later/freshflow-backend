using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.FileClaim;

public sealed record FileClaimCommand(
    Guid UserId,
    Guid OrderId,
    decimal Amount,
    string Reason,
    string? ProofImageUrl = null) : ICommand<OrderClaimDto>;
