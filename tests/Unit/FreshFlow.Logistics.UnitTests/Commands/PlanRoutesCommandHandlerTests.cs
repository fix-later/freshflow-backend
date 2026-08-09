using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Commands.PlanRoutes;
using FreshFlow.Logistics.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class PlanRoutesCommandHandlerTests
{
    [Fact]
    public async Task Handle_NoAtHubOrders_ReturnsEmptyWithoutMatrixCallAsync()
    {
        var hubId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 10);
        var inputs = Substitute.For<IRoutePlanningInputBuilder>();
        inputs.BuildAsync(hubId, date, Arg.Any<CancellationToken>()).Returns(
            FreshFlow.SharedKernel.Application.Result<RoutePlanningInput>.Success(new RoutePlanningInput(
                hubId, "Hub", 10m, 106m, date, [], [], "revision")));
        var matrices = Substitute.For<IRouteMatrixProvider>();
        var handler = new PlanRoutesCommandHandler(
            inputs, matrices, Substitute.For<IRoutePlanningSolver>(),
            Substitute.For<IRoutePlanRepository>(), Substitute.For<IDeliveryRouteRepository>(),
            Substitute.For<IVehicleCapacityPolicy>(), NullLogger<PlanRoutesCommandHandler>.Instance);

        var result = await handler.Handle(new PlanRoutesCommand(hubId, date, "TIME"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("empty");
        result.Value.PlanId.Should().BeNull();
        result.Value.OptimizationCriteria.Should().Be(nameof(OptimizationCriteria.time).ToUpperInvariant());
        await matrices.DidNotReceiveWithAnyArgs().GetMatrixAsync(default!, default!, default);
    }
}
