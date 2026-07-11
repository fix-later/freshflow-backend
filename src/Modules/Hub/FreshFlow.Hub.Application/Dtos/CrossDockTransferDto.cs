namespace FreshFlow.Hub.Application.Dtos;

public sealed record CrossDockTransferDto(
    Guid CrossDockId,
    Guid HubId,
    Guid InboundEventId,
    Guid OutboundRouteId,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
