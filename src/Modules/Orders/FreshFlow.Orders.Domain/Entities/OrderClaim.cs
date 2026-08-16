using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

public sealed class OrderClaim : AggregateRoot
{
    private OrderClaim() { }

    public OrderClaim(
        Guid orderId,
        Guid restaurantId,
        decimal amount,
        string reason,
        Guid createdBy,
        DateTime createdAt,
        string? proofImageUrl = null)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order id is required.", nameof(orderId));
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("Creator id is required.", nameof(createdBy));
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        OrderId = orderId;
        RestaurantId = restaurantId;
        Amount = amount;
        Reason = reason.Trim();
        ProofImageUrl = string.IsNullOrWhiteSpace(proofImageUrl) ? null : proofImageUrl.Trim();
        Status = OrderClaimStatus.Submitted;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid OrderId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? ProofImageUrl { get; private set; }
    public OrderClaimStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? DecisionNote { get; private set; }
    public Guid? RefundTransactionId { get; private set; }

    public Result Approve(
        Guid reviewedBy,
        DateTime reviewedAt,
        string? decisionNote,
        Guid refundTransactionId)
    {
        if (Status == OrderClaimStatus.Approved)
            return Result.Success();
        if (Status != OrderClaimStatus.Submitted)
            return InvalidTransition();
        if (reviewedBy == Guid.Empty || refundTransactionId == Guid.Empty)
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "Reviewer and refund transaction are required."));

        Status = OrderClaimStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        DecisionNote = Normalize(decisionNote);
        RefundTransactionId = refundTransactionId;
        UpdatedAt = reviewedAt;
        return Result.Success();
    }

    public Result Reject(Guid reviewedBy, DateTime reviewedAt, string decisionNote)
    {
        if (Status != OrderClaimStatus.Submitted)
            return InvalidTransition();
        if (reviewedBy == Guid.Empty)
            return Result.Failure(Error.Validation("VALIDATION_ERROR", "Reviewer is required."));
        if (string.IsNullOrWhiteSpace(decisionNote))
            return Result.Failure(Error.Validation(
                "INVALID_CLAIM_DECISION_NOTE",
                "A rejection decision note is required."));

        Status = OrderClaimStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        DecisionNote = decisionNote.Trim();
        UpdatedAt = reviewedAt;
        return Result.Success();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result InvalidTransition() =>
        Result.Failure(Error.Conflict(
            "CLAIM_INVALID_TRANSITION",
            "The claim is already in a terminal state."));
}
