using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrderIssueTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderItemId = Guid.NewGuid();
    private static readonly Guid ReportedBy = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidArgs_SetsOpenIssueAndTrimsDescription()
    {
        var issue = new OrderIssue(
            OrderId,
            OrderItemId,
            ReportedBy,
            OrderIssueType.Damaged,
            affectedQuantity: 2.5m,
            description: "  damaged crates  ");

        issue.OrderId.Should().Be(OrderId);
        issue.OrderItemId.Should().Be(OrderItemId);
        issue.ReportedBy.Should().Be(ReportedBy);
        issue.IssueType.Should().Be(OrderIssueType.Damaged);
        issue.AffectedQuantity.Should().Be(2.5m);
        issue.Description.Should().Be("damaged crates");
        issue.Status.Should().Be(OrderIssueStatus.Open);
        issue.ResolvedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidAffectedQuantity_Throws(decimal affectedQuantity)
    {
        var act = () => new OrderIssue(
            OrderId,
            OrderItemId,
            ReportedBy,
            OrderIssueType.Missing,
            affectedQuantity,
            "missing item");

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("affectedQuantity");
    }

    [Fact]
    public void Resolve_OpenIssue_SetsResolvedStatusAndTimestamp()
    {
        var issue = new OrderIssue(
            OrderId,
            null,
            ReportedBy,
            OrderIssueType.Wrong,
            affectedQuantity: 1m,
            description: "wrong product");
        var resolvedAt = new DateTime(2026, 6, 18, 8, 0, 0, DateTimeKind.Utc);

        var result = issue.Resolve(resolvedAt);

        result.IsSuccess.Should().BeTrue();
        issue.Status.Should().Be(OrderIssueStatus.Resolved);
        issue.ResolvedAt.Should().Be(resolvedAt);
    }

    [Fact]
    public void Resolve_AlreadyResolved_ReturnsConflict()
    {
        var issue = new OrderIssue(
            OrderId,
            null,
            ReportedBy,
            OrderIssueType.Wrong,
            affectedQuantity: 1m,
            description: "wrong product");
        issue.Resolve();

        var result = issue.Resolve();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ISSUE_ALREADY_RESOLVED");
    }
}
