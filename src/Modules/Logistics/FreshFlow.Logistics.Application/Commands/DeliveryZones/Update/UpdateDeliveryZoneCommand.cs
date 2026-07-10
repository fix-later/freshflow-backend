using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;

public sealed record UpdateDeliveryZoneCommand(
    Guid Id,
    string Name,
    string? Description) : ICommand<DeliveryZoneDto>;
