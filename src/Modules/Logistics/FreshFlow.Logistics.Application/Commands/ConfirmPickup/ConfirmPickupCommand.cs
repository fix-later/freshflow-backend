using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.ConfirmPickup;

public sealed record ConfirmPickupCommand(
    Guid RouteId,
    Guid DriverUserId,
    IReadOnlyList<Guid> OrderIds) : ICommand<ConfirmPickupResultDto>;
