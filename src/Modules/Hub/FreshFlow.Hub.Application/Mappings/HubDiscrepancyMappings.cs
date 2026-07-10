using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Mappings;

internal static class HubDiscrepancyMappings
{
    public static HubDiscrepancyDto ToDto(this HubDiscrepancy discrepancy) =>
        new(
            discrepancy.Id,
            discrepancy.HubId,
            discrepancy.InboundEventId,
            discrepancy.OrderId,
            discrepancy.OrderItemId,
            discrepancy.AffectedQuantity,
            discrepancy.ConditionStatus,
            discrepancy.Notes,
            discrepancy.Status,
            discrepancy.AcknowledgedBy,
            discrepancy.AcknowledgedAt,
            discrepancy.CreatedAt,
            discrepancy.UpdatedAt);
}
