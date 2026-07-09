using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;

public sealed record CreateDeliveryZoneCommand(
    string Code,
    string Name,
    string? Description) : ICommand<DeliveryZoneDto>;
