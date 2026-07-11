namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubDiscrepancyDto(
    Guid DiscrepancyId,
    Guid HubId,
    Guid InboundEventId,
    Guid OrderId,
    Guid OrderItemId,
    decimal AffectedQuantity,
    string ConditionStatus,
    string? Notes,
    string Status,
    Guid? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
