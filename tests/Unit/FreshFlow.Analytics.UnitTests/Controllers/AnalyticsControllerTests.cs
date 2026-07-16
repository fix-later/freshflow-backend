using System.Reflection;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.API.Controllers;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}

