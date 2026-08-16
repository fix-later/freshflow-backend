using FreshFlow.Hub.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubDiscrepancy : AggregateRoot
{
    public const string ConditionMissing = "MISSING";
    public const string ConditionDamaged = "DAMAGED";
    public const string ConditionPartial = "PARTIAL";
    public const string StatusOpen = "OPEN";
    public const string StatusAcknowledged = "ACKNOWLEDGED";

    private static readonly string[] ValidConditions =
    [
        ConditionMissing,
        ConditionDamaged,
        ConditionPartial
    ];

    private HubDiscrepancy() { } // EF Core

    public Guid HubId { get; private set; }
    public Guid InboundEventId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public decimal AffectedQuantity { get; private set; }
    public string ConditionStatus { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string? ProofImageUrl { get; private set; }
    public string Status { get; private set; } = StatusOpen;
    public Guid? AcknowledgedBy { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }

    public static HubDiscrepancy Create(
        Guid hubId,
        Guid inboundEventId,
        Guid orderId,
        Guid orderItemId,
        decimal affectedQuantity,
        string conditionStatus,
        string? notes,
        string? proofImageUrl = null)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));
        if (inboundEventId == Guid.Empty)
            throw new ArgumentException("Inbound event id is required.", nameof(inboundEventId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id is required.", nameof(orderItemId));
        if (affectedQuantity <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(affectedQuantity),
                affectedQuantity,
                "Affected quantity must be greater than zero.");
        if (!ValidConditions.Contains(conditionStatus))
            throw new ArgumentException("Invalid condition status.", nameof(conditionStatus));

        var now = DateTime.UtcNow;
        var discrepancy = new HubDiscrepancy
        {
            HubId = hubId,
            InboundEventId = inboundEventId,
            OrderId = orderId,
            OrderItemId = orderItemId,
            AffectedQuantity = affectedQuantity,
            ConditionStatus = conditionStatus,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ProofImageUrl = string.IsNullOrWhiteSpace(proofImageUrl) ? null : proofImageUrl.Trim(),
            Status = StatusOpen,
            CreatedAt = now,
            UpdatedAt = now
        };

        discrepancy.RaiseDomainEvent(new HubDiscrepancyRecordedDomainEvent(
            discrepancy.Id,
            discrepancy.HubId,
            discrepancy.InboundEventId,
            discrepancy.OrderId,
            discrepancy.OrderItemId,
            discrepancy.AffectedQuantity,
            discrepancy.ConditionStatus,
            now));

        return discrepancy;
    }

    public void Acknowledge(Guid adminUserId)
    {
        if (adminUserId == Guid.Empty)
            throw new ArgumentException("Admin user id is required.", nameof(adminUserId));
        if (Status != StatusOpen)
            throw new InvalidOperationException("Discrepancy has already been acknowledged.");

        Status = StatusAcknowledged;
        AcknowledgedBy = adminUserId;
        AcknowledgedAt = DateTime.UtcNow;
        UpdatedAt = AcknowledgedAt.Value;
    }
}
