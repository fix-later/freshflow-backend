namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubInboundDto(
    Guid InboundId,
    Guid HubId,
    Guid? SourceMarketId,
    Guid? DeliveryRouteId,
    Guid? DeliveryScheduleId,
    IReadOnlyList<HubInboundItemDto> Items,
    decimal TotalQuantityKg,
    DateTime ArrivedAt,
    Guid? RecordedBy,
    Guid? HubStaffUserId,
    string Status,
    string ConditionStatus,
    string? DiscrepancyNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
