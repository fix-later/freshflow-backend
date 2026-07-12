namespace FreshFlow.Logistics.Domain.Entities;

public sealed class Delivery
{
    public const string StatusPending = "pending";
    public const string StatusArrived = "arrived";
    public const string StatusDelivered = "delivered";
    public const string StatusFailed = "failed";

    private Delivery() { } // EF Core

    public Guid Id { get; private set; }
    public Guid DeliveryRouteId { get; private set; }
    public Guid OrderId { get; private set; }
    public int SequenceNumber { get; private set; }
    public string Status { get; private set; } = StatusPending;
    public DateTime? EstimatedArrival { get; private set; }
    public DateTime? ActualArrival { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProofUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Delivery Create(
        Guid deliveryRouteId,
        Guid orderId,
        int sequenceNumber,
        DateTime? estimatedArrival = null)
    {
        if (deliveryRouteId == Guid.Empty)
            throw new ArgumentException("Delivery route id is required.", nameof(deliveryRouteId));

        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));

        if (sequenceNumber <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "Sequence number must be greater than zero.");

        var now = DateTime.UtcNow;
        return new Delivery
        {
            Id = Guid.NewGuid(),
            DeliveryRouteId = deliveryRouteId,
            OrderId = orderId,
            SequenceNumber = sequenceNumber,
            Status = StatusPending,
            EstimatedArrival = estimatedArrival,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkArrived()
    {
        EnsureNotTerminal();

        Status = StatusArrived;
        ActualArrival = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDelivered(DateTime actualArrival)
    {
        EnsureNotTerminal();

        Status = StatusDelivered;
        ActualArrival = actualArrival;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        EnsureNotTerminal();

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason is required.", nameof(reason));

        Status = StatusFailed;
        FailureReason = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void AttachProof(string proofUrl)
    {
        if (string.IsNullOrWhiteSpace(proofUrl))
            throw new ArgumentException("Proof URL is required.", nameof(proofUrl));

        ProofUrl = proofUrl.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureNotTerminal()
    {
        if (Status is StatusDelivered or StatusFailed)
            throw new InvalidOperationException("Delivery is already completed.");
    }
}
