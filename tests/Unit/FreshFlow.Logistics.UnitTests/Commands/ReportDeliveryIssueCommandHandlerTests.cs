using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReportDeliveryIssueCommandHandlerTests
{
    [Fact]
    public async Task Handle_HappyPath_CreatesOpenIssueAsync()
    {
        var driverId = Guid.NewGuid();
        var route = CreateAssignedRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var (deliveries, _, issues, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new ReportDeliveryIssueCommand(delivery.Id, driverId, "DAMAGED", "  box torn  "),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryId.Should().Be(delivery.Id);
        result.Value.IssueType.Should().Be(DeliveryIssue.TypeDamaged);
        result.Value.Description.Should().Be("box torn");
        result.Value.Status.Should().Be(DeliveryIssue.StatusOpen);
        issues.Issues.Should().ContainSingle(issue =>
            issue.Id == result.Value.Id &&
            issue.ReportedBy == driverId);
        issues.SaveChangesCount.Should().Be(1);
        deliveries.Deliveries.Should().Contain(delivery);
    }

    [Fact]
    public async Task Handle_DeliveryNotFound_Returns404Async()
    {
        var sut = new ReportDeliveryIssueCommandHandler(
            new InMemoryDeliveryRepository(),
            new InMemoryDeliveryRouteRepository(),
            new InMemoryDeliveryIssueRepository());

        var result = await sut.Handle(
            new ReportDeliveryIssueCommand(Guid.NewGuid(), Guid.NewGuid(), "damaged", "issue"),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DELIVERY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RouteNotFound_Returns404Async()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        var deliveries = new InMemoryDeliveryRepository();
        await deliveries.AddRangeAsync([delivery], default);
        var issues = new InMemoryDeliveryIssueRepository();
        var sut = new ReportDeliveryIssueCommandHandler(
            deliveries,
            new InMemoryDeliveryRouteRepository(),
            issues);

        var result = await sut.Handle(
            new ReportDeliveryIssueCommand(delivery.Id, Guid.NewGuid(), "damaged", "issue"),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DELIVERY_ROUTE_NOT_FOUND");
        issues.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DeliveryAssignedToAnotherDriver_ReturnsForbiddenAsync()
    {
        var route = CreateAssignedRoute(Guid.NewGuid());
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var (_, _, issues, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new ReportDeliveryIssueCommand(delivery.Id, Guid.NewGuid(), "damaged", "issue"),
            default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FORBIDDEN");
        issues.Issues.Should().BeEmpty();
        issues.SaveChangesCount.Should().Be(0);
    }

    [Theory]
    [InlineData(Delivery.StatusPending)]
    [InlineData(Delivery.StatusArrived)]
    [InlineData(Delivery.StatusDelivered)]
    [InlineData(Delivery.StatusFailed)]
    public async Task Handle_AllDeliveryStatuses_CanReportIssueAsync(string status)
    {
        var driverId = Guid.NewGuid();
        var route = CreateAssignedRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        MoveDeliveryTo(delivery, status);
        var (_, _, issues, sut) = await CreateSutAsync(route, [delivery]);

        var result = await sut.Handle(
            new ReportDeliveryIssueCommand(delivery.Id, driverId, "other", "note"),
            default);

        result.IsSuccess.Should().BeTrue();
        issues.Issues.Should().ContainSingle();
    }

    private static async Task<(
        InMemoryDeliveryRepository Deliveries,
        InMemoryDeliveryRouteRepository Routes,
        InMemoryDeliveryIssueRepository Issues,
        ReportDeliveryIssueCommandHandler Sut)> CreateSutAsync(
            DeliveryRoute route,
            IReadOnlyList<Delivery> routeDeliveries)
    {
        var deliveries = new InMemoryDeliveryRepository();
        var routes = new InMemoryDeliveryRouteRepository();
        var issues = new InMemoryDeliveryIssueRepository();
        await routes.AddAsync(route, default);
        await deliveries.AddRangeAsync(routeDeliveries, default);
        return (deliveries, routes, issues, new ReportDeliveryIssueCommandHandler(deliveries, routes, issues));
    }

    private static DeliveryRoute CreateAssignedRoute(Guid driverId)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 12),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
        route.ApplyOptimization(route.Stops, 10m, 20, 50000m, OptimizationCriteria.distance);
        route.Select();
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }

    private static void MoveDeliveryTo(Delivery delivery, string status)
    {
        if (status == Delivery.StatusArrived)
            delivery.MarkArrived();
        else if (status == Delivery.StatusDelivered)
            delivery.MarkDelivered(DateTime.UtcNow);
        else if (status == Delivery.StatusFailed)
            delivery.MarkFailed("failed");
    }
}
