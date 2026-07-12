using FluentAssertions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class DeliveryIssueTests
{
    [Fact]
    public void Create_ValidInput_ReturnsOpenIssueWithTrimmedDescription()
    {
        var deliveryId = Guid.NewGuid();
        var reportedBy = Guid.NewGuid();

        var issue = DeliveryIssue.Create(deliveryId, reportedBy, "DAMAGED", "  damaged crate  ");

        issue.Id.Should().NotBeEmpty();
        issue.DeliveryId.Should().Be(deliveryId);
        issue.ReportedBy.Should().Be(reportedBy);
        issue.IssueType.Should().Be(DeliveryIssue.TypeDamaged);
        issue.Description.Should().Be("damaged crate");
        issue.Status.Should().Be(DeliveryIssue.StatusOpen);
        issue.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        issue.UpdatedAt.Should().Be(issue.CreatedAt);
    }

    [Fact]
    public void Create_EmptyIds_ThrowsArgumentException()
    {
        var deliveryId = Guid.NewGuid();
        var reportedBy = Guid.NewGuid();

        Action emptyDelivery = () => DeliveryIssue.Create(Guid.Empty, reportedBy, "damaged", "issue");
        Action emptyReporter = () => DeliveryIssue.Create(deliveryId, Guid.Empty, "damaged", "issue");

        emptyDelivery.Should().Throw<ArgumentException>();
        emptyReporter.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("missing")]
    public void Create_InvalidIssueType_ThrowsArgumentException(string issueType)
    {
        Action act = () => DeliveryIssue.Create(Guid.NewGuid(), Guid.NewGuid(), issueType, "issue");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyDescription_ThrowsArgumentException(string description)
    {
        Action act = () => DeliveryIssue.Create(Guid.NewGuid(), Guid.NewGuid(), "damaged", description);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_DescriptionTooLong_ThrowsArgumentException()
    {
        var description = new string('x', DeliveryIssue.MaxDescriptionLength + 1);

        Action act = () => DeliveryIssue.Create(Guid.NewGuid(), Guid.NewGuid(), "damaged", description);

        act.Should().Throw<ArgumentException>();
    }
}
