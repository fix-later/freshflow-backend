using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;

public sealed record DeactivateDeliveryZoneCommand(Guid Id) : ICommand<DeliveryZoneDto>;
