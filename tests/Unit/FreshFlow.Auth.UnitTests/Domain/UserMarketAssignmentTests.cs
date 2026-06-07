using FluentAssertions;
using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class UserMarketAssignmentTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var assignedBy = Guid.NewGuid();

        var assignment = new UserMarketAssignment(userId, marketId, assignedBy);

        assignment.Id.Should().NotBe(Guid.Empty);
        assignment.UserId.Should().Be(userId);
        assignment.MarketId.Should().Be(marketId);
        assignment.AssignedBy.Should().Be(assignedBy);
        assignment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        assignment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_AssignedBy_CanBeNull()
    {
        var assignment = new UserMarketAssignment(Guid.NewGuid(), Guid.NewGuid(), null);

        assignment.AssignedBy.Should().BeNull();
    }
}
