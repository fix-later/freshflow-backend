using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetRestaurantCredit;

internal sealed class GetRestaurantCreditQueryHandler(
    ICreditRepository creditRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<GetRestaurantCreditQuery, Result<RestaurantCreditDto>>
{
    public async Task<Result<RestaurantCreditDto>> Handle(
        GetRestaurantCreditQuery request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByIdAsync(request.RestaurantId, cancellationToken);
        if (restaurant is null)
            return Result<RestaurantCreditDto>.Failure(Error.NotFound("Restaurant", request.RestaurantId));

        if (!request.IsAdmin)
        {
            var ownedRestaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (ownedRestaurant is null || ownedRestaurant.RestaurantId != request.RestaurantId)
                return Result<RestaurantCreditDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This restaurant credit account is not accessible."));
        }

        var account = await creditRepository.FindAccountAsync(request.RestaurantId, cancellationToken)
                      ?? new RestaurantCredit(request.RestaurantId);

        return Result<RestaurantCreditDto>.Success(CreditDtoMapper.ToDto(account));
    }
}
