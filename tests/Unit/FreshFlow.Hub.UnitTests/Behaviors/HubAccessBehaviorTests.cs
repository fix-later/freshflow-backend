using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Behaviors;
using FreshFlow.Hub.Application.Commands.CreateDiscrepancyProofUploadSignature;
using FreshFlow.Hub.Application.Commands.CreateCrossDock;
using FreshFlow.Hub.Application.Commands.CreateHandover;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.RecordOutbound;
using FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;
using FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.ListCrossDock;
using FreshFlow.Hub.Application.Queries.ListDiscrepancies;
using FreshFlow.Hub.Application.Queries.ListHandovers;
using FreshFlow.Hub.Application.Queries.ListInbound;
using FreshFlow.Hub.Application.Queries.ListOutbound;
using FreshFlow.Hub.Application.Services;
using FreshFlow.Hub.UnitTests.TestDoubles;
using FreshFlow.SharedKernel.Application;
using MediatR;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Behaviors;

[Trait("Category", "Unit")]
public sealed class HubAccessBehaviorTests
{
    [Theory]
    [InlineData(typeof(RecordInboundCommand))]
    [InlineData(typeof(GetPendingInboundQuery))]
    [InlineData(typeof(ListInboundQuery))]
    [InlineData(typeof(RecordDiscrepancyCommand))]
    [InlineData(typeof(CreateDiscrepancyProofUploadSignatureCommand))]
    [InlineData(typeof(ListDiscrepanciesQuery))]
    [InlineData(typeof(CreateCrossDockCommand))]
    [InlineData(typeof(ListCrossDockQuery))]
    [InlineData(typeof(RecordOutboundCommand))]
    [InlineData(typeof(ListOutboundQuery))]
    [InlineData(typeof(CreateHandoverCommand))]
    [InlineData(typeof(ListHandoversQuery))]
    public void OperationalRequest_IsCoveredBySharedHubAccessGuard(Type requestType)
    {
        typeof(IHubAccessRequest).IsAssignableFrom(requestType).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(GetHubStaffAssignmentsQuery))]
    [InlineData(typeof(ReplaceHubStaffAssignmentsCommand))]
    public void ManagementRequest_IsCoveredByCurrentPrivilegeGuard(Type requestType)
    {
        typeof(IHubManagementRequest).IsAssignableFrom(requestType).Should().BeTrue();
    }

    [Fact]
    public async Task AssignedStaff_ContinuesPipelineAsync()
    {
        var actorUserId = Guid.NewGuid();
        var (hubs, assignments, hub) = await CreateAsync();
        await assignments.ReplaceAsync(hub.Id, [actorUserId], default);
        var sut = CreateBehavior(hubs, assignments);

        var result = await sut.Handle(
            new TestHubRequest(hub.Id, actorUserId, false),
            _ => Task.FromResult(Result<bool>.Success(true)),
            default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UnassignedStaff_ReturnsStrictForbiddenWithoutCallingHandlerAsync()
    {
        var (hubs, assignments, hub) = await CreateAsync();
        var handlerCalled = false;
        var sut = CreateBehavior(hubs, assignments);

        var result = await sut.Handle(
            new TestHubRequest(hub.Id, Guid.NewGuid(), false),
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult(Result<bool>.Success(true));
            },
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_ACCESS_DENIED");
        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task BypassRole_ContinuesForActiveHubWithoutAssignmentAsync()
    {
        var actorUserId = Guid.NewGuid();
        var (hubs, assignments, hub) = await CreateAsync();
        var staff = new InMemoryHubStaffReader
        {
            Users = [new HubStaffUserDto(actorUserId, "admin", true, null)]
        };
        var sut = CreateBehavior(hubs, assignments, staff);

        var result = await sut.Handle(
            new TestHubRequest(hub.Id, actorUserId, true),
            _ => Task.FromResult(Result<bool>.Success(true)),
            default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InactiveHub_DeniesEvenBypassRoleAsync()
    {
        var (hubs, assignments, hub) = await CreateAsync();
        hub.Deactivate();
        var sut = CreateBehavior(hubs, assignments);

        var result = await sut.Handle(
            new TestHubRequest(hub.Id, Guid.NewGuid(), true),
            _ => Task.FromResult(Result<bool>.Success(true)),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_ACCESS_DENIED");
    }

    [Theory]
    [InlineData("hub_staff", false)]
    [InlineData("driver", true)]
    public async Task StaleHubStaffToken_DeniesInactiveOrWrongRoleUserAsync(
        string role,
        bool isActive)
    {
        var actorUserId = Guid.NewGuid();
        var (hubs, assignments, hub) = await CreateAsync();
        await assignments.ReplaceAsync(hub.Id, [actorUserId], default);
        var staff = new InMemoryHubStaffReader
        {
            Users = [new HubStaffUserDto(actorUserId, role, isActive, null)]
        };
        var sut = CreateBehavior(hubs, assignments, staff);

        var result = await sut.Handle(
            new TestHubRequest(hub.Id, actorUserId, false),
            _ => Task.FromResult(Result<bool>.Success(true)),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_ACCESS_DENIED");
    }

    [Theory]
    [InlineData("admin", false)]
    [InlineData("driver", true)]
    public async Task StalePrivilegedToken_DeniesOperationalAndManagementRequestsAsync(
        string role,
        bool isActive)
    {
        var actorUserId = Guid.NewGuid();
        var (hubs, assignments, hub) = await CreateAsync();
        var staff = new InMemoryHubStaffReader
        {
            Users = [new HubStaffUserDto(actorUserId, role, isActive, null)]
        };
        var checker = new HubAccessChecker(assignments, staff);
        var operational = new HubAccessBehavior<TestHubRequest, Result<bool>>(hubs, checker);
        var management = new HubAccessBehavior<TestManagementRequest, Result<bool>>(hubs, checker);

        var operationalResult = await operational.Handle(
            new TestHubRequest(hub.Id, actorUserId, true),
            _ => Task.FromResult(Result<bool>.Success(true)),
            default);
        var managementResult = await management.Handle(
            new TestManagementRequest(actorUserId),
            _ => Task.FromResult(Result<bool>.Success(true)),
            default);

        operationalResult.Error.Code.Should().Be("HUB_ACCESS_DENIED");
        managementResult.Error.Code.Should().Be("HUB_ACCESS_DENIED");
    }

    private static HubAccessBehavior<TestHubRequest, Result<bool>> CreateBehavior(
        InMemoryHubRepository hubs,
        InMemoryHubStaffAssignmentRepository assignments,
        InMemoryHubStaffReader? staff = null) =>
        new(hubs, new HubAccessChecker(
            assignments,
            staff ?? new InMemoryHubStaffReader { AllowAll = true }));

    private static async Task<(
        InMemoryHubRepository Hubs,
        InMemoryHubStaffAssignmentRepository Assignments,
        HubEntity Hub)> CreateAsync()
    {
        var hubs = new InMemoryHubRepository();
        var assignments = new InMemoryHubStaffAssignmentRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        return (hubs, assignments, hub);
    }

    private sealed record TestHubRequest(
        Guid HubId,
        Guid ActorUserId,
        bool BypassHubAssignment)
        : IRequest<Result<bool>>, IHubAccessRequest;

    private sealed record TestManagementRequest(Guid ActorUserId)
        : IRequest<Result<bool>>, IHubManagementRequest;
}
