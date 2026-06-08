using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Update;

public sealed record UpdateDeliveryAddressCommand(
    Guid UserId,
    Guid AddressId,
    string? RecipientName,
    string? Phone,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault) : ICommand<UpdateDeliveryAddressResponse>;
