using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Update;

internal sealed class UpdateDeliveryAddressCommandHandler(
    IRestaurantRepository restaurants,
    IDeliveryAddressRepository addresses)
    : IRequestHandler<UpdateDeliveryAddressCommand, Result<UpdateDeliveryAddressResponse>>
{
    public async Task<Result<UpdateDeliveryAddressResponse>> Handle(
        UpdateDeliveryAddressCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result<UpdateDeliveryAddressResponse>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        var existing = await addresses.FindByIdAndRestaurantIdAsync(
            request.AddressId, restaurant.Id, ct);
        if (existing is null)
            return Result<UpdateDeliveryAddressResponse>.Failure(
                new Error("DELIVERY_ADDRESS_NOT_FOUND",
                    $"Delivery address '{request.AddressId}' was not found."));

        if (request.IsDefault)
            await addresses.ClearDefaultsAsync(restaurant.Id, ct);

        var dto = await addresses.UpdateAsync(
            request.AddressId,
            request.RecipientName,
            request.Phone,
            request.AddressLine,
            request.Latitude,
            request.Longitude,
            request.IsDefault,
            ct);

        return Result<UpdateDeliveryAddressResponse>.Success(ToResponse(dto));
    }

    private static UpdateDeliveryAddressResponse ToResponse(DeliveryAddressDto dto) =>
        new(dto.Id, dto.RestaurantId, dto.RecipientName, dto.Phone,
            dto.AddressLine, dto.Latitude, dto.Longitude,
            dto.IsDefault, dto.CreatedAt, dto.UpdatedAt);
}
