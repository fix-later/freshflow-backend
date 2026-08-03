using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetClaimById;

internal sealed class GetClaimByIdQueryHandler(
    IOrderClaimRepository claims,
    IRestaurantReader restaurants)
    : IRequestHandler<GetClaimByIdQuery, Result<OrderClaimDto>>
{
    public async Task<Result<OrderClaimDto>> Handle(
        GetClaimByIdQuery request,
        CancellationToken cancellationToken)
    {
        var claim = await claims.FindByIdAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return Result<OrderClaimDto>.Failure(Error.NotFound("CLAIM", request.ClaimId));

        if (!request.IsPrivileged)
        {
            var restaurant = await restaurants.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null || restaurant.RestaurantId != claim.RestaurantId)
                return Result<OrderClaimDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This claim is not accessible."));
        }

        return Result<OrderClaimDto>.Success(OrderClaimDtoMapper.ToDto(claim));
    }
}
