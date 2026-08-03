using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ApproveClaim;

internal sealed class ApproveClaimCommandHandler(
    IOrderRepository orders,
    IOrderClaimRepository claims,
    ICreditService credit)
    : IRequestHandler<ApproveClaimCommand, Result<OrderClaimDto>>
{
    public async Task<Result<OrderClaimDto>> Handle(
        ApproveClaimCommand request,
        CancellationToken cancellationToken)
    {
        OrderClaimDto? response = null;
        var transaction = await orders.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var claim = await claims.FindByIdAsync(request.ClaimId, ct);
            if (claim is null)
                return Result.Failure(Error.NotFound("CLAIM", request.ClaimId));

            if (claim.Status == OrderClaimStatus.Approved)
            {
                response = OrderClaimDtoMapper.ToDto(claim);
                return Result.Success();
            }

            if (claim.Status != OrderClaimStatus.Submitted)
                return Result.Failure(Error.Conflict(
                    "CLAIM_INVALID_TRANSITION",
                    "The claim is already in a terminal state."));

            var note = string.IsNullOrWhiteSpace(request.DecisionNote)
                ? "Claim approved"
                : $"Claim approved: {request.DecisionNote.Trim()}";
            var refund = await credit.RefundAsync(
                claim.RestaurantId,
                claim.OrderId,
                claim.Amount,
                note,
                ct);
            if (refund.IsFailure)
                return Result.Failure(refund.Error);

            var approval = claim.Approve(
                request.ReviewedBy,
                DateTime.UtcNow,
                request.DecisionNote,
                refund.Value.TransactionId);
            if (approval.IsFailure)
                return approval;

            claims.Track(claim);
            response = OrderClaimDtoMapper.ToDto(claim);
            return Result.Success();
        }, cancellationToken);

        return transaction.IsFailure
            ? Result<OrderClaimDto>.Failure(transaction.Error)
            : Result<OrderClaimDto>.Success(response!);
    }
}
