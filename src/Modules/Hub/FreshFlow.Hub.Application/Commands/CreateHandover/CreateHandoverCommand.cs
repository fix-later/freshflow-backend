using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.CreateHandover;

public sealed record CreateHandoverCommand(
    Guid HubId,
    Guid DeliveryRouteId,
    Guid DriverUserId,
    Guid? OutboundEventId,
    Guid HandedOverBy,
    string? Notes = null) : ICommand<HubHandoverDto>;
