using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Delete;

internal sealed class DeleteDeliveryAddressCommandHandler(
    IRestaurantRepository restaurants,
    IDeliveryAddressRepository addresses)
    : IRequestHandler<DeleteDeliveryAddressCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDeliveryAddressCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result.Failure(Error.NotFound("Restaurant", request.UserId));

        var existing = await addresses.FindByIdAndRestaurantIdAsync(
            request.AddressId, restaurant.Id, ct);
        if (existing is null)
            return Result.Failure(
                new Error("DELIVERY_ADDRESS_NOT_FOUND",
                    $"Delivery address '{request.AddressId}' was not found."));

        await addresses.SoftDeleteAsync(request.AddressId, restaurant.Id, ct);
        return Result.Success();
    }
}
