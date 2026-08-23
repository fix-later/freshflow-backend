using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.ApproveRoutePlan;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ApproveRoutePlanCommandHandlerTests
{
    private readonly IRoutePlanRepository _plans = Substitute.For<IRoutePlanRepository>();
    private readonly IRoutePlanningInputBuilder _inputs = Substitute.For<IRoutePlanningInputBuilder>();
    private readonly IDeliveryRepository _deliveries = Substitute.For<IDeliveryRepository>();
    private readonly ApproveRoutePlanCommandHandler _sut;

    private static readonly Guid HubId = Guid.NewGuid();
    private static readonly DateOnly ServiceDate = new(2026, 8, 10);
    private const string Revision = "revision";

    public ApproveRoutePlanCommandHandlerTests()
    {
        _sut = new ApproveRoutePlanCommandHandler(
            _plans, _inputs, _deliveries, NullLogger<ApproveRoutePlanCommandHandler>.Instance);
    }

    private RoutePlan MakePlan(IReadOnlyList<RoutePlanUnassigned> unassigned) =>
        RoutePlan.Create(
            Guid.NewGuid(), HubId, ServiceDate, OptimizationCriteria.distance,
            "GOONG", false, Revision, unassigned, vehiclesUsed: 0,
            totalLoadKg: 0m, totalDistanceKm: 0m, estimatedDurationMinutes: 0, estimatedCost: 0m);

    private void SetupMatchingInput(RoutePlan plan)
    {
        _plans.FindByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        _inputs.BuildAsync(plan.HubId, plan.ServiceDate, Arg.Any<CancellationToken>())
            .Returns(FreshFlow.SharedKernel.Application.Result<RoutePlanningInput>.Success(
                new RoutePlanningInput(HubId, "Hub", 10m, 106m, ServiceDate, [], [], Revision, [])));
        _plans.GetRoutesAsync(plan.Id, Arg.Any<CancellationToken>())
            .Returns(new List<DeliveryRoute>());
        _plans.TrySaveChangesAsync(Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task Handle_OnlyIncompleteDataEntries_IsApprovableAsync()
    {
        // Arrange — every Unassigned entry is a builder-side exclusion, not a solver one
        var plan = MakePlan([
            new RoutePlanUnassigned(Guid.NewGuid(), "R1", [Guid.NewGuid()], 0m,
                "Order has no checkout delivery coordinates.", ExcludedForIncompleteData: true)
        ]);
        SetupMatchingInput(plan);

        // Act
        var result = await _sut.Handle(new ApproveRoutePlanCommand(plan.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        plan.Status.Should().Be(RoutePlanStatus.approved);
    }

    [Fact]
    public async Task Handle_SolverProducedEntryPresent_StillBlocksApprovalAsync()
    {
        // Arrange — a solver "didn't fit" entry (default ExcludedForIncompleteData = false)
        var plan = MakePlan([
            new RoutePlanUnassigned(Guid.NewGuid(), "R1", [Guid.NewGuid()], 60m,
                "FLEET_CAPACITY_UNAVAILABLE")
        ]);
        SetupMatchingInput(plan);

        // Act
        var result = await _sut.Handle(new ApproveRoutePlanCommand(plan.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PLAN_HAS_UNASSIGNED_ORDERS");
        plan.Status.Should().Be(RoutePlanStatus.proposed);
    }

    [Fact]
    public async Task Handle_MixOfBothKinds_StillBlocksApprovalAsync()
    {
        // Arrange — one incomplete-data entry (harmless) plus one solver entry (blocking)
        var plan = MakePlan([
            new RoutePlanUnassigned(Guid.NewGuid(), "R1", [Guid.NewGuid()], 0m,
                "Restaurant record is unavailable.", ExcludedForIncompleteData: true),
            new RoutePlanUnassigned(Guid.NewGuid(), "R2", [Guid.NewGuid()], 60m,
                "FLEET_CAPACITY_UNAVAILABLE")
        ]);
        SetupMatchingInput(plan);

        // Act
        var result = await _sut.Handle(new ApproveRoutePlanCommand(plan.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PLAN_HAS_UNASSIGNED_ORDERS");
    }
}
