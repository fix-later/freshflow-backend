namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubOutboundEventDto(
    Guid OutboundId,
    Guid HubId,
    Guid DestinationRouteId,
    IReadOnlyList<HubOutboundItemDto> Items,
    decimal TotalQuantityKg,
    DateTime DispatchedAt,
    Guid? RecordedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);
