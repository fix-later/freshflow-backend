namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubHandoverDto(
    Guid HandoverId,
    Guid HubId,
    Guid DeliveryRouteId,
    Guid DriverUserId,
    Guid? OutboundEventId,
    string Status,
    Guid HandedOverBy,
    DateTime HandedOverAt,
    DateTime? DriverConfirmedAt,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
