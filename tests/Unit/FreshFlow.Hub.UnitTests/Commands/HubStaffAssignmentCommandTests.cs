using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubStaffAssignmentCommandTests
{
    [Fact]
    public async Task Replace_ValidHubStaff_ReplacesWholeListAsync()
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        var staff = new InMemoryHubStaffReader
        {
            Users =
            [
                new HubStaffUserDto(first, "hub_staff", true, null),
                new HubStaffUserDto(second, "hub_staff", true, null)
            ]
        };
        var sut = new ReplaceHubStaffAssignmentsCommandHandler(hubs, staff, assignments);

        var result = await sut.Handle(
            new ReplaceHubStaffAssignmentsCommand(hub.Id, [first, second], Guid.NewGuid()),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.StaffUserIds.Should().Equal(first, second);
        assignments.ReplaceCallCount.Should().Be(1);
        (await assignments.GetUserIdsByHubAsync(hub.Id, default))
            .Should().BeEquivalentTo([first, second]);
    }

    [Fact]
    public async Task Replace_EmptyList_ClearsAssignmentsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        await assignments.ReplaceAsync(hub.Id, [Guid.NewGuid()], default);
        var sut = new ReplaceHubStaffAssignmentsCommandHandler(
            hubs,
            new InMemoryHubStaffReader(),
            assignments);

        var result = await sut.Handle(
            new ReplaceHubStaffAssignmentsCommand(hub.Id, [], Guid.NewGuid()),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.StaffUserIds.Should().BeEmpty();
        (await assignments.GetUserIdsByHubAsync(hub.Id, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Replace_MissingUser_ReturnsNotFoundWithoutReplacingAsync()
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var sut = new ReplaceHubStaffAssignmentsCommandHandler(
            hubs,
            new InMemoryHubStaffReader(),
            assignments);

        var result = await sut.Handle(
            new ReplaceHubStaffAssignmentsCommand(hub.Id, [Guid.NewGuid()], Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("USER_NOT_FOUND");
        assignments.ReplaceCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("hub_staff", false)]
    public async Task Replace_IneligibleUser_ReturnsInvalidTargetAsync(string role, bool isActive)
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var userId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        var staff = new InMemoryHubStaffReader
        {
            Users = [new HubStaffUserDto(userId, role, isActive, null)]
        };
        var sut = new ReplaceHubStaffAssignmentsCommandHandler(hubs, staff, assignments);

        var result = await sut.Handle(
            new ReplaceHubStaffAssignmentsCommand(hub.Id, [userId], Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ASSIGNMENT_TARGET");
        assignments.ReplaceCallCount.Should().Be(0);
    }

    [Fact]
    public void Validator_DuplicateOrEmptyUserIds_Fails()
    {
        var userId = Guid.NewGuid();
        var sut = new ReplaceHubStaffAssignmentsCommandValidator();

        var duplicate = sut.Validate(
            new ReplaceHubStaffAssignmentsCommand(
                Guid.NewGuid(), [userId, userId], Guid.NewGuid()));
        var empty = sut.Validate(
            new ReplaceHubStaffAssignmentsCommand(
                Guid.NewGuid(), [Guid.Empty], Guid.NewGuid()));

        duplicate.IsValid.Should().BeFalse();
        empty.IsValid.Should().BeFalse();
    }
}
