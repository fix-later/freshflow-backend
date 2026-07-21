using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Queries.GetAssignedHubs;
using FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;
using FreshFlow.Hub.Application.Services;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class HubStaffAssignmentQueryTests
{
    [Fact]
    public async Task GetAssignments_ExistingHub_ReturnsAssignedUserIdsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var userId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await assignments.ReplaceAsync(hub.Id, [userId], default);
        var sut = new GetHubStaffAssignmentsQueryHandler(hubs, assignments);

        var result = await sut.Handle(
            new GetHubStaffAssignmentsQuery(hub.Id, Guid.NewGuid()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().Be(hub.Id);
        result.Value.StaffUserIds.Should().Equal(userId);
    }

    [Fact]
    public async Task GetAssignments_MissingHub_ReturnsNotFoundAsync()
    {
        var sut = new GetHubStaffAssignmentsQueryHandler(
            new InMemoryHubRepository(),
            new InMemoryHubStaffAssignmentRepository());

        var result = await sut.Handle(
            new GetHubStaffAssignmentsQuery(Guid.NewGuid(), Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }

    [Fact]
    public async Task GetAssignedHubs_ReturnsRepositoryActiveHubsAsync()
    {
        var hub = HubEntity.Create("Assigned Hub", null, null, null, 1000, null);
        var assignments = new InMemoryHubStaffAssignmentRepository
        {
            AssignedHubs = [hub]
        };
        var userId = Guid.NewGuid();
        var access = new HubAccessChecker(
            assignments,
            new InMemoryHubStaffReader { AllowAll = true });
        var sut = new GetAssignedHubsQueryHandler(assignments, access);

        var result = await sut.Handle(new GetAssignedHubsQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(item => item.HubId == hub.Id);
    }

    [Fact]
    public async Task GetAssignedHubs_InactiveUserWithStaleToken_ReturnsForbiddenAsync()
    {
        var userId = Guid.NewGuid();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var access = new HubAccessChecker(
            assignments,
            new InMemoryHubStaffReader
            {
                Users = [new HubStaffUserDto(userId, "hub_staff", false, null)]
            });
        var sut = new GetAssignedHubsQueryHandler(assignments, access);

        var result = await sut.Handle(new GetAssignedHubsQuery(userId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_ACCESS_DENIED");
    }
}
