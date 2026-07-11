using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.CreateCrossDock;

public sealed record CreateCrossDockCommand(
    Guid HubId,
    Guid InboundEventId,
    Guid OutboundRouteId,
    string? Notes = null) : ICommand<CrossDockTransferDto>;
