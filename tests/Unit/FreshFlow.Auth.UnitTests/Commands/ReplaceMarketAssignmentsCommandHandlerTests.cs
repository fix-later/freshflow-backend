using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReplaceMarketAssignmentsCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IMarketValidator _marketValidator = Substitute.For<IMarketValidator>();
    private readonly IUserMarketAssignmentRepository _assignments = Substitute.For<IUserMarketAssignmentRepository>();
    private readonly ReplaceMarketAssignmentsCommandHandler _sut;

    private static User MakeMarketAgent()
    {
        var role = new Role("market_agent", "Market Agent");
        return User.Create("agent@test.com", "hash", role);
    }

    private static User MakeDriver()
    {
        var role = new Role("driver", "Driver");
        return User.Create("driver@test.com", "hash", role);
    }

    public ReplaceMarketAssignmentsCommandHandlerTests()
    {
        _sut = new ReplaceMarketAssignmentsCommandHandler(_users, _marketValidator, _assignments);
    }

    [Fact]
    public async Task Handle_ValidReplaceWithTwoMarkets_ReturnsUpdatedAssignments()
    {
        // Arrange
        var agent = MakeMarketAgent();
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();

        _users.FindByIdAsync(agent.Id, default).Returns(agent);
        _marketValidator.IsActiveMarketAsync(m1, default).Returns(true);
        _marketValidator.IsActiveMarketAsync(m2, default).Returns(true);
        _assignments.GetByUserIdAsync(agent.Id, default).Returns(
            (IReadOnlyList<UserMarketAssignment>)[]);

        var cmd = new ReplaceMarketAssignmentsCommand(agent.Id, [m1, m2], AssignedById: null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(agent.Id);
        result.Value.MarketIds.Should().BeEquivalentTo(new[] { m1, m2 });
        await _assignments.Received(2)
            .AddAsync(Arg.Any<UserMarketAssignment>(), default);
        await _assignments.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_EmptyMarketIds_ClearsAllAssignments()
    {
        // Arrange
        var agent = MakeMarketAgent();
        var existing = new UserMarketAssignment(agent.Id, Guid.NewGuid(), null);
        _users.FindByIdAsync(agent.Id, default).Returns(agent);
        _assignments.GetByUserIdAsync(agent.Id, default).Returns(
            (IReadOnlyList<UserMarketAssignment>)[existing]);

        var cmd = new ReplaceMarketAssignmentsCommand(agent.Id, [], AssignedById: null);

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MarketIds.Should().BeEmpty();
        await _assignments.Received(1)
            .RemoveRangeAsync(Arg.Is<IEnumerable<UserMarketAssignment>>(a => a.Single() == existing), default);
        await _assignments.DidNotReceive().AddAsync(Arg.Any<UserMarketAssignment>(), default);
    }

    [Fact]
    public async Task Handle_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _users.FindByIdAsync(id, default).Returns((User?)null);

        // Act
        var result = await _sut.Handle(
            new ReplaceMarketAssignmentsCommand(id, [], null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UserNotMarketAgent_ReturnsInvalidAssignmentTarget()
    {
        // Arrange
        var driver = MakeDriver();
        _users.FindByIdAsync(driver.Id, default).Returns(driver);

        // Act
        var result = await _sut.Handle(
            new ReplaceMarketAssignmentsCommand(driver.Id, [], null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ASSIGNMENT_TARGET");
    }

    [Fact]
    public async Task Handle_InactiveMarket_ReturnsInvalidMarket()
    {
        // Arrange
        var agent = MakeMarketAgent();
        var badMarket = Guid.NewGuid();
        _users.FindByIdAsync(agent.Id, default).Returns(agent);
        _marketValidator.IsActiveMarketAsync(badMarket, default).Returns(false);

        // Act
        var result = await _sut.Handle(
            new ReplaceMarketAssignmentsCommand(agent.Id, [badMarket], null), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_MARKET");
    }

    [Fact]
    public async Task Handle_ReplacesExistingAssignments_RemovesOldBeforeAddingNew()
    {
        // Arrange
        var agent = MakeMarketAgent();
        var oldMarket = Guid.NewGuid();
        var newMarket = Guid.NewGuid();
        var oldAssignment = new UserMarketAssignment(agent.Id, oldMarket, null);

        _users.FindByIdAsync(agent.Id, default).Returns(agent);
        _marketValidator.IsActiveMarketAsync(newMarket, default).Returns(true);
        _assignments.GetByUserIdAsync(agent.Id, default).Returns(
            (IReadOnlyList<UserMarketAssignment>)[oldAssignment]);

        // Act
        var result = await _sut.Handle(
            new ReplaceMarketAssignmentsCommand(agent.Id, [newMarket], null), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _assignments.Received(1).RemoveRangeAsync(
            Arg.Is<IEnumerable<UserMarketAssignment>>(a => a.Contains(oldAssignment)), default);
        await _assignments.Received(1).AddAsync(
            Arg.Is<UserMarketAssignment>(a => a.MarketId == newMarket), default);
    }
}
