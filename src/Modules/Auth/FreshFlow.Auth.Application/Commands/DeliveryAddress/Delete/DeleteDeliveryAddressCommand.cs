using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Delete;

public sealed record DeleteDeliveryAddressCommand(
    Guid UserId,
    Guid AddressId) : ICommand;
