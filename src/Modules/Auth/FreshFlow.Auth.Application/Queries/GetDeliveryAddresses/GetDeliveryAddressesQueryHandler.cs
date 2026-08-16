using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetDeliveryAddresses;

internal sealed class GetDeliveryAddressesQueryHandler(
    IRestaurantRepository restaurants,
    IDeliveryAddressRepository addresses)
    : IRequestHandler<GetDeliveryAddressesQuery, Result<IReadOnlyList<DeliveryAddressDto>>>
{
    public async Task<Result<IReadOnlyList<DeliveryAddressDto>>> Handle(
        GetDeliveryAddressesQuery request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result<IReadOnlyList<DeliveryAddressDto>>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        var list = await addresses.GetByRestaurantIdAsync(restaurant.Id, ct);
        return Result<IReadOnlyList<DeliveryAddressDto>>.Success(list);
    }
}
