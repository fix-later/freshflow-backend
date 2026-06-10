using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Queries.GetMarketAssignments;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetMarketAssignmentsQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUserMarketAssignmentRepository _assignments = Substitute.For<IUserMarketAssignmentRepository>();
    private readonly GetMarketAssignmentsQueryHandler _sut;

    public GetMarketAssignmentsQueryHandlerTests()
    {
        _sut = new GetMarketAssignmentsQueryHandler(_users, _assignments);
    }

    [Fact]
    public async Task Handle_ExistingAgentWithTwoMarkets_ReturnsBothMarketIds()
    {
        // Arrange
        var role = new Role("market_agent", "Market Agent");
        var agent = User.Create("a@test.com", "h", role);
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        _users.FindByIdAsync(agent.Id, default).Returns(agent);
        _assignments.GetByUserIdAsync(agent.Id, default).Returns(
            (IReadOnlyList<UserMarketAssignment>)
            [
                new UserMarketAssignment(agent.Id, m1, null),
                new UserMarketAssignment(agent.Id, m2, null),
            ]);

        // Act
        var result = await _sut.Handle(new GetMarketAssignmentsQuery(agent.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(agent.Id);
        result.Value.MarketIds.Should().BeEquivalentTo(new[] { m1, m2 });
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _users.FindByIdAsync(id, default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(new GetMarketAssignmentsQuery(id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UserWithNoAssignments_ReturnsEmptyList()
    {
        // Arrange
        var role = new Role("market_agent", "Market Agent");
        var agent = User.Create("b@test.com", "h", role);

        _users.FindByIdAsync(agent.Id, default).Returns(agent);
        _assignments.GetByUserIdAsync(agent.Id, default).Returns(
            (IReadOnlyList<UserMarketAssignment>)[]);

        // Act
        var result = await _sut.Handle(new GetMarketAssignmentsQuery(agent.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MarketIds.Should().BeEmpty();
    }
}
