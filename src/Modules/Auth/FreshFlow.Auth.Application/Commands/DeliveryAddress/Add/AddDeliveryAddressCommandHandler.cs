using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;

internal sealed class AddDeliveryAddressCommandHandler(
    IRestaurantRepository restaurants,
    IDeliveryAddressRepository addresses)
    : IRequestHandler<AddDeliveryAddressCommand, Result<AddDeliveryAddressResponse>>
{
    public async Task<Result<AddDeliveryAddressResponse>> Handle(
        AddDeliveryAddressCommand request, CancellationToken ct)
    {
        var restaurant = await restaurants.FindByUserIdAsync(request.UserId, ct);
        if (restaurant is null)
            return Result<AddDeliveryAddressResponse>.Failure(
                Error.NotFound("Restaurant", request.UserId));

        if (request.IsDefault)
            await addresses.ClearDefaultsAsync(restaurant.Id, ct);

        var dto = await addresses.AddAsync(
            restaurant.Id,
            request.RecipientName,
            request.Phone,
            request.AddressLine,
            request.Latitude,
            request.Longitude,
            request.IsDefault,
            ct);

        return Result<AddDeliveryAddressResponse>.Success(ToResponse(dto));
    }

    private static AddDeliveryAddressResponse ToResponse(DeliveryAddressDto dto) =>
        new(dto.Id, dto.RestaurantId, dto.RecipientName, dto.Phone,
            dto.AddressLine, dto.Latitude, dto.Longitude,
            dto.IsDefault, dto.CreatedAt, dto.UpdatedAt);
}
