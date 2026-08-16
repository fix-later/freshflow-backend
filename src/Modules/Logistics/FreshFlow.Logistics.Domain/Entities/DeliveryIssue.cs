namespace FreshFlow.Logistics.Domain.Entities;

public sealed class DeliveryIssue
{
    public const string TypeUndeliverable = "undeliverable";
    public const string TypeDamaged = "damaged";
    public const string TypeCustomerRejected = "customer_rejected";
    public const string TypeOther = "other";
    public const string StatusOpen = "open";
    public const string StatusResolved = "resolved";
    public const int MaxDescriptionLength = 1000;

    private static readonly HashSet<string> AllowedTypes =
    [
        TypeUndeliverable,
        TypeDamaged,
        TypeCustomerRejected,
        TypeOther
    ];

    private DeliveryIssue() { } // EF Core

    public Guid Id { get; private set; }
    public Guid DeliveryId { get; private set; }
    public string IssueType { get; private set; } = TypeOther;
    public string Description { get; private set; } = string.Empty;
    public string Status { get; private set; } = StatusOpen;
    public Guid ReportedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static DeliveryIssue Create(
        Guid deliveryId,
        Guid reportedBy,
        string issueType,
        string description)
    {
        if (deliveryId == Guid.Empty)
            throw new ArgumentException("Delivery id is required.", nameof(deliveryId));

        if (reportedBy == Guid.Empty)
            throw new ArgumentException("Reporter id is required.", nameof(reportedBy));

        if (string.IsNullOrWhiteSpace(issueType))
            throw new ArgumentException("Issue type is required.", nameof(issueType));

        var normalizedType = issueType.Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(normalizedType))
            throw new ArgumentException("Issue type is invalid.", nameof(issueType));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > MaxDescriptionLength)
            throw new ArgumentException("Description is too long.", nameof(description));

        var now = DateTime.UtcNow;
        return new DeliveryIssue
        {
            Id = Guid.NewGuid(),
            DeliveryId = deliveryId,
            IssueType = normalizedType,
            Description = trimmedDescription,
            Status = StatusOpen,
            ReportedBy = reportedBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
