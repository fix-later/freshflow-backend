using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class OrderIssue : BaseEntity
{
    public const int MaxDescriptionLength = 1000;

    private OrderIssue() { } // EF Core

    public OrderIssue(
        Guid orderId,
        Guid? orderItemId,
        Guid reportedBy,
        OrderIssueType issueType,
        decimal affectedQuantity,
        string description)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));

        if (orderItemId == Guid.Empty)
            throw new ArgumentException("Order item id must be null or a non-empty id.", nameof(orderItemId));

        if (reportedBy == Guid.Empty)
            throw new ArgumentException("Reporter id is required.", nameof(reportedBy));

        if (affectedQuantity <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(affectedQuantity), affectedQuantity, "Affected quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > MaxDescriptionLength)
            throw new ArgumentOutOfRangeException(
                nameof(description), trimmedDescription.Length, $"Description must be {MaxDescriptionLength} characters or fewer.");

        OrderId = orderId;
        OrderItemId = orderItemId;
        ReportedBy = reportedBy;
        IssueType = issueType;
        AffectedQuantity = affectedQuantity;
        Description = trimmedDescription;
        Status = OrderIssueStatus.Open;
    }

    public Guid OrderId { get; private set; }
    public Guid? OrderItemId { get; private set; }
    public Guid ReportedBy { get; private set; }
    public OrderIssueType IssueType { get; private set; }
    public decimal AffectedQuantity { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public OrderIssueStatus Status { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public Result Resolve(DateTime? resolvedAtUtc = null)
    {
        if (Status == OrderIssueStatus.Resolved)
            return Result.Failure(Error.Conflict(
                "ORDER_ISSUE_ALREADY_RESOLVED", "This order issue has already been resolved."));

        Status = OrderIssueStatus.Resolved;
        ResolvedAt = resolvedAtUtc ?? DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        return Result.Success();
    }
}
