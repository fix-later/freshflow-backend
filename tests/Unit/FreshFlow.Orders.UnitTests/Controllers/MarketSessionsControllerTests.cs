using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessions;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class MarketSessionsControllerTests
{
    [Fact]
    public void GetAvailabilityAsync_IsRestrictedToRestaurant()
    {
        var authorize = typeof(MarketSessionsController)
            .GetMethod(nameof(MarketSessionsController.GetAvailabilityAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be("restaurant");
    }

    [Theory]
    [InlineData("open", true)]
    [InlineData("closed", false)]
    [InlineData(null, false)]
    public async Task GetAvailabilityAsync_ReturnsWhetherSelectedMarketSessionIsOpen(
        string? status, bool expectedIsOpen)
    {
        var sender = Substitute.For<ISender>();
        var marketId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 8, 16);
        var session = status is null ? null : Session(marketId, serviceDate, status);
        sender.Send(Arg.Any<GetMarketSessionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MarketSessionDto>>.Success(
                session is null ? [] : [session]));

        var response = await new MarketSessionsController(sender)
            .GetAvailabilityAsync(marketId, serviceDate, default);

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)
            .Should().BeOfType<MarketSessionAvailabilityResponse>().Subject;
        data.Exists.Should().Be(session is not null);
        data.IsOpen.Should().Be(expectedIsOpen);
        data.Status.Should().Be(status);
        await sender.Received(1).Send(
            Arg.Is<GetMarketSessionsQuery>(query =>
                query.MarketId == marketId &&
                query.From == serviceDate &&
                query.To == serviceDate),
            Arg.Any<CancellationToken>());
    }

    private static MarketSessionDto Session(Guid marketId, DateOnly serviceDate, string status) => new(
        Guid.NewGuid(), marketId, null, null, serviceDate, status, DateTime.UtcNow,
        null, null, null, null, 0, 0, 0, 0, null, [], [], "ready", [],
        DateTime.UtcNow, DateTime.UtcNow);
}
