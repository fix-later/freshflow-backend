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
    public async Task Handle_NoRoutableOrders_ReturnsEmptyWithoutMatrixCallAsync()
    {
        var hubId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 10);
        var inputs = Substitute.For<IRoutePlanningInputBuilder>();
        inputs.BuildAsync(hubId, date, Arg.Any<CancellationToken>()).Returns(
            FreshFlow.SharedKernel.Application.Result<RoutePlanningInput>.Success(new RoutePlanningInput(
                hubId, "Hub", 10m, 106m, date, [], [], "revision", [])));
        var matrices = Substitute.For<IRouteMatrixProvider>();
        var sessions = Substitute.For<IMarketSessionReader>();
        sessions.FindByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new MarketSessionLookupDto(sessionId, hubId, date, "Closed"));
        var handler = new PlanRoutesCommandHandler(
            inputs, sessions, matrices, Substitute.For<IRoutePlanningSolver>(),
            Substitute.For<IRoutePlanRepository>(), Substitute.For<IDeliveryRouteRepository>(),
            Substitute.For<IVehicleCapacityPolicy>(), NullLogger<PlanRoutesCommandHandler>.Instance);

        var result = await handler.Handle(new PlanRoutesCommand(sessionId, "TIME"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("empty");
        result.Value.PlanId.Should().BeNull();
        result.Value.OptimizationCriteria.Should().Be(nameof(OptimizationCriteria.time).ToUpperInvariant());
        await matrices.DidNotReceiveWithAnyArgs().GetMatrixAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_NumericCriteria_ReturnsValidationErrorAsync()
    {
        var inputs = Substitute.For<IRoutePlanningInputBuilder>();
        var handler = new PlanRoutesCommandHandler(
            inputs,
            Substitute.For<IMarketSessionReader>(),
            Substitute.For<IRouteMatrixProvider>(),
            Substitute.For<IRoutePlanningSolver>(),
            Substitute.For<IRoutePlanRepository>(),
            Substitute.For<IDeliveryRouteRepository>(),
            Substitute.For<IVehicleCapacityPolicy>(),
            NullLogger<PlanRoutesCommandHandler>.Instance);

        var result = await handler.Handle(
            new PlanRoutesCommand(Guid.NewGuid(), "7"),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await inputs.DidNotReceiveWithAnyArgs().BuildAsync(default, default, default);
    }

    [Fact]
    public async Task Handle_MissingMarketSession_ReturnsNotFoundAsync()
    {
        var sessionId = Guid.NewGuid();
        var inputs = Substitute.For<IRoutePlanningInputBuilder>();
        var sessions = Substitute.For<IMarketSessionReader>();
        sessions.FindByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((MarketSessionLookupDto?)null);
        var handler = new PlanRoutesCommandHandler(
            inputs,
            sessions,
            Substitute.For<IRouteMatrixProvider>(),
            Substitute.For<IRoutePlanningSolver>(),
            Substitute.For<IRoutePlanRepository>(),
            Substitute.For<IDeliveryRouteRepository>(),
            Substitute.For<IVehicleCapacityPolicy>(),
            NullLogger<PlanRoutesCommandHandler>.Instance);

        var result = await handler.Handle(new PlanRoutesCommand(sessionId, null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("MARKET_SESSION_NOT_FOUND");
        await inputs.DidNotReceiveWithAnyArgs().BuildAsync(default, default, default);
    }
}
