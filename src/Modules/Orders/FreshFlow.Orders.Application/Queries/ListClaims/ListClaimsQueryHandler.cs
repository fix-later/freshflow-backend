using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.ListClaims;

internal sealed class ListClaimsQueryHandler(
    IOrderClaimRepository claims,
    IRestaurantReader restaurants)
    : IRequestHandler<ListClaimsQuery, Result<OrderClaimPageDto>>
{
    public async Task<Result<OrderClaimPageDto>> Handle(
        ListClaimsQuery request,
        CancellationToken cancellationToken)
    {
        var restaurantFilter = request.RestaurantId;
        if (!request.IsPrivileged)
        {
            var restaurant = await restaurants.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null)
                return Result<OrderClaimPageDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));
            if (restaurantFilter.HasValue && restaurantFilter.Value != restaurant.RestaurantId)
                return Result<OrderClaimPageDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This restaurant's claims are not accessible."));

            restaurantFilter = restaurant.RestaurantId;
        }

        if (!OrderClaimQueryParsing.TryParseStatus(request.Status, out var status))
            return Result<OrderClaimPageDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "Status is invalid."));

        var (items, nextCursor) = await claims.SearchAsync(
            new OrderClaimSearchCriteria(
                restaurantFilter,
                status,
                request.Cursor,
                request.PageSize),
            cancellationToken);

        return Result<OrderClaimPageDto>.Success(new OrderClaimPageDto(
            items.Select(OrderClaimDtoMapper.ToDto).ToList().AsReadOnly(),
            request.PageSize,
            nextCursor));
    }
}
