using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.RejectClaim;

internal sealed class RejectClaimCommandHandler(
    IOrderRepository orders,
    IOrderClaimRepository claims)
    : IRequestHandler<RejectClaimCommand, Result<OrderClaimDto>>
{
    public async Task<Result<OrderClaimDto>> Handle(
        RejectClaimCommand request,
        CancellationToken cancellationToken)
    {
        OrderClaimDto? response = null;
        var transaction = await orders.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var claim = await claims.FindByIdAsync(request.ClaimId, ct);
            if (claim is null)
                return Result.Failure(Error.NotFound("CLAIM", request.ClaimId));

            var rejection = claim.Reject(
                request.ReviewedBy,
                DateTime.UtcNow,
                request.DecisionNote);
            if (rejection.IsFailure)
                return rejection;

            claims.Track(claim);
            response = OrderClaimDtoMapper.ToDto(claim);
            return Result.Success();
        }, cancellationToken);

        return transaction.IsFailure
            ? Result<OrderClaimDto>.Failure(transaction.Error)
            : Result<OrderClaimDto>.Success(response!);
    }
}
