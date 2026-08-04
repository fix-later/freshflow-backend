using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrderClaimTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly DateTime SubmittedAt = new(2026, 8, 4, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_CreatesAuditableSubmittedClaim()
    {
        var claim = NewClaim();

        claim.OrderId.Should().Be(OrderId);
        claim.RestaurantId.Should().Be(RestaurantId);
        claim.Amount.Should().Be(50_000m);
        claim.Reason.Should().Be("Damaged produce");
        claim.Status.Should().Be(OrderClaimStatus.Submitted);
        claim.CreatedBy.Should().Be(CreatorId);
        claim.CreatedAt.Should().Be(SubmittedAt);
        claim.ReviewedBy.Should().BeNull();
        claim.ReviewedAt.Should().BeNull();
        claim.RefundTransactionId.Should().BeNull();
    }

    [Fact]
    public void Approve_RecordsReviewerTimestampNoteAndRefund()
    {
        var claim = NewClaim();
        var reviewerId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var reviewedAt = SubmittedAt.AddHours(1);

        var result = claim.Approve(reviewerId, reviewedAt, "Verified", refundId);

        result.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(OrderClaimStatus.Approved);
        claim.ReviewedBy.Should().Be(reviewerId);
        claim.ReviewedAt.Should().Be(reviewedAt);
        claim.DecisionNote.Should().Be("Verified");
        claim.RefundTransactionId.Should().Be(refundId);
        claim.UpdatedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void Reject_RecordsDecisionWithoutRefund()
    {
        var claim = NewClaim();
        var reviewerId = Guid.NewGuid();
        var reviewedAt = SubmittedAt.AddHours(1);

        var result = claim.Reject(reviewerId, reviewedAt, "No supporting evidence");

        result.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(OrderClaimStatus.Rejected);
        claim.ReviewedBy.Should().Be(reviewerId);
        claim.ReviewedAt.Should().Be(reviewedAt);
        claim.DecisionNote.Should().Be("No supporting evidence");
        claim.RefundTransactionId.Should().BeNull();
    }

    [Fact]
    public void ApprovedClaim_ReapproveIsNoOp_RejectIsConflict()
    {
        var claim = NewClaim();
        var reviewerId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var reviewedAt = SubmittedAt.AddHours(1);
        claim.Approve(reviewerId, reviewedAt, "Verified", refundId);

        var repeated = claim.Approve(Guid.NewGuid(), reviewedAt.AddHours(1), "Changed", Guid.NewGuid());
        var rejected = claim.Reject(Guid.NewGuid(), reviewedAt.AddHours(1), "Changed");

        repeated.IsSuccess.Should().BeTrue();
        rejected.IsFailure.Should().BeTrue();
        rejected.Error.Code.Should().Be("CLAIM_INVALID_TRANSITION");
        claim.ReviewedBy.Should().Be(reviewerId);
        claim.ReviewedAt.Should().Be(reviewedAt);
        claim.DecisionNote.Should().Be("Verified");
        claim.RefundTransactionId.Should().Be(refundId);
    }

    [Fact]
    public void RejectedClaim_CannotApproveOrRejectAgain()
    {
        var claim = NewClaim();
        claim.Reject(Guid.NewGuid(), SubmittedAt.AddHours(1), "Rejected");

        claim.Approve(Guid.NewGuid(), SubmittedAt.AddHours(2), null, Guid.NewGuid())
            .Error.Code.Should().Be("CLAIM_INVALID_TRANSITION");
        claim.Reject(Guid.NewGuid(), SubmittedAt.AddHours(2), "Again")
            .Error.Code.Should().Be("CLAIM_INVALID_TRANSITION");
    }

    private static OrderClaim NewClaim() =>
        new(OrderId, RestaurantId, 50_000m, " Damaged produce ", CreatorId, SubmittedAt);
}
