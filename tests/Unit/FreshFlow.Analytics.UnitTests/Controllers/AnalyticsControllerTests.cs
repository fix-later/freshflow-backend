using System.Reflection;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;
using FreshFlow.Analytics.Application.Queries.GetHubThroughput;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.Analytics.Application.Queries.GetProcurementMetrics;
using FreshFlow.API.Controllers;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NSubstitute;

namespace FreshFlow.Analytics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class AnalyticsControllerTests
{
    [Fact]
    public void Controller_RequiresAuthentication_AndOverviewRequiresOperationsRole()
    {
        var classAuthorize = typeof(AnalyticsController).GetCustomAttribute<AuthorizeAttribute>();
        var actionAuthorize = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetOverviewAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        classAuthorize.Should().NotBeNull();
        classAuthorize!.Roles.Should().BeNull();
        actionAuthorize.Should().NotBeNull();
        actionAuthorize!.Roles.Should().Be("admin,operations_manager");
        actionAuthorize.Roles.Should().NotContain("restaurant");
    }

    [Fact]
    public void PriceTrends_AllowsRealAdminOperationsAndRestaurantRoles()
    {
        var actionAuthorize = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetPriceTrendsAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        actionAuthorize.Should().NotBeNull();
        actionAuthorize!.Roles.Should().Be("admin,operations_manager,restaurant");
    }

    [Fact]
    public async Task GetOverviewAsync_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var date = new DateOnly(2026, 7, 16);
        var dto = new DashboardOverviewDto(1, 100m, 1, 0, 0, 0, 0m, 0m, 0m);
        sender.Send(Arg.Any<GetDashboardOverviewQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DashboardOverviewDto>.Success(dto));
        var controller = new AnalyticsController(sender);

        var response = await controller.GetOverviewAsync(date, default);

        response.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetDashboardOverviewQuery>(query => query.Date == date),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPriceTrendsAsync_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var marketProductId = Guid.NewGuid();
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 16);
        sender.Send(Arg.Any<GetPriceTrendsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PriceTrendsDto>.Success(new PriceTrendsDto([])));
        var controller = new AnalyticsController(sender);

        var response = await controller.GetPriceTrendsAsync(
            [marketProductId],
            from,
            to,
            "hourly",
            default);

        response.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetPriceTrendsQuery>(query =>
                query.MarketProductIds.SequenceEqual(new[] { marketProductId }) &&
                query.From == from && query.To == to && query.Interval == "hourly"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OrderMetrics_RequiresOperationsRoleAndRequiredDateBounds()
    {
        var method = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetOrderMetricsAsync))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        var parameters = method.GetParameters();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("admin,operations_manager");
        authorize.Roles.Should().NotContain("restaurant");
        parameters.Single(parameter => parameter.Name == "from")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
        parameters.Single(parameter => parameter.Name == "to")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrderMetricsAsync_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var restaurantId = Guid.NewGuid();
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 16);
        var dto = new OrderMetricsDto(
            new OrderMetricsSummaryDto(0, 0m, 0m, 0, 0m, 0, new Dictionary<string, int>()),
            []);
        sender.Send(Arg.Any<GetOrderMetricsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<OrderMetricsDto>.Success(dto));
        var controller = new AnalyticsController(sender);

        var response = await controller.GetOrderMetricsAsync(
            from,
            to,
            restaurantId,
            "week",
            default);

        response.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetOrderMetricsQuery>(query =>
                query.From == from &&
                query.To == to &&
                query.RestaurantId == restaurantId &&
                query.GroupBy == "week"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ProcurementMetrics_RequiresOperationsRoleAndRequiredDateBounds()
    {
        var method = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetProcurementMetricsAsync))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        var parameters = method.GetParameters();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("admin,operations_manager");
        authorize.Roles.Should().NotContain("restaurant");
        parameters.Single(parameter => parameter.Name == "from")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
        parameters.Single(parameter => parameter.Name == "to")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetProcurementMetricsAsync_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var marketId = Guid.NewGuid();
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 16);
        var dto = new ProcurementMetricsDto(
            0,
            new Dictionary<string, int>(),
            0m,
            0,
            0,
            0,
            0m,
            null,
            null,
            0,
            new Dictionary<string, int>());
        sender.Send(Arg.Any<GetProcurementMetricsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<ProcurementMetricsDto>.Success(dto));
        var controller = new AnalyticsController(sender);

        var response = await controller.GetProcurementMetricsAsync(
            from,
            to,
            marketId,
            default);

        response.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetProcurementMetricsQuery>(query =>
                query.From == from &&
                query.To == to &&
                query.MarketId == marketId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void HubThroughput_AllowsHubStaffAndRequiresDateBounds()
    {
        var method = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetHubThroughputAsync))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        var parameters = method.GetParameters();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("admin,operations_manager,hub_staff");
        authorize.Roles.Should().NotContain("restaurant");
        parameters.Single(parameter => parameter.Name == "from")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
        parameters.Single(parameter => parameter.Name == "to")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetHubThroughputAsync_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 16);
        var dto = new HubThroughputDto(
            new HubThroughputSummaryDto(0m, 0m, 0m, 0, 0, new Dictionary<string, int>()),
            []);
        sender.Send(Arg.Any<GetHubThroughputQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubThroughputDto>.Success(dto));
        var controller = new AnalyticsController(sender);

        var response = await controller.GetHubThroughputAsync(from, to, hubId, default);

        response.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetHubThroughputQuery>(query =>
                query.From == from && query.To == to && query.HubId == hubId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeliveryPerformance_RequiresOperationsRoleAndRequiredDateBounds()
    {
        var method = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetDeliveryPerformanceAsync))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        var parameters = method.GetParameters();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("admin,operations_manager");
        authorize.Roles.Should().NotContain("restaurant");
        authorize.Roles.Should().NotContain("driver");
        parameters.Single(parameter => parameter.Name == "from")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
        parameters.Single(parameter => parameter.Name == "to")
            .GetCustomAttribute<BindRequiredAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetDeliveryPerformanceAsync_SendsQueryAndReturnsOkAsync()
    {
        var sender = Substitute.For<ISender>();
        var from = new DateOnly(2026, 7, 1);
        var to = new DateOnly(2026, 7, 16);
        var dto = new DeliveryPerformanceDto(0, 0, 0, 0m, 0, null, 0, null, 0);
        sender.Send(Arg.Any<GetDeliveryPerformanceQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeliveryPerformanceDto>.Success(dto));
        var controller = new AnalyticsController(sender);

        var response = await controller.GetDeliveryPerformanceAsync(from, to, default);

        response.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetDeliveryPerformanceQuery>(query =>
                query.From == from && query.To == to),
            Arg.Any<CancellationToken>());
    }
}

