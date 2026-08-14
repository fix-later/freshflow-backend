using FluentAssertions;
using FreshFlow.Hub.Application.Queries.GetHubDriverAssignments;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class HubDriverAssignmentQueryTests
{
    [Fact]
    public async Task GetAssignments_ExistingHub_ReturnsAssignedUserIdsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubDriverAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var userId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await assignments.ReplaceAsync(hub.Id, [userId], default);
        var sut = new GetHubDriverAssignmentsQueryHandler(hubs, assignments);

        var result = await sut.Handle(
            new GetHubDriverAssignmentsQuery(hub.Id, Guid.NewGuid()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().Be(hub.Id);
        result.Value.DriverUserIds.Should().Equal(userId);
    }

    [Fact]
    public async Task GetAssignments_MissingHub_ReturnsNotFoundAsync()
    {
        var sut = new GetHubDriverAssignmentsQueryHandler(
            new InMemoryHubRepository(),
            new InMemoryHubDriverAssignmentRepository());

        var result = await sut.Handle(
            new GetHubDriverAssignmentsQuery(Guid.NewGuid(), Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }
}
