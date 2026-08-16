using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;

public sealed record AddDeliveryAddressCommand(
    Guid UserId,
    string? RecipientName,
    string? Phone,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault) : ICommand<AddDeliveryAddressResponse>;
