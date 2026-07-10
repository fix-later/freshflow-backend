using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.ScanInbound;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.ListInbound;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubInboundControllerTests
{
    [Fact]
    public void HubInboundController_RequiresHubStaffAdminOrOperationsManagerAuthorization()
    {
        var attr = typeof(HubInboundController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("hub_staff,admin,operations_manager");
    }

    [Fact]
    public async Task RecordInboundAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var sourceMarketId = Guid.NewGuid();
        var deliveryScheduleId = Guid.NewGuid();
        var marketProductId = Guid.NewGuid();
        var arrivedAt = DateTime.UtcNow;
        sender.Send(Arg.Any<RecordInboundCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubInboundDto>.Success(CreateDto(hubId)));
        var controller = new HubInboundController(sender);

        var result = await controller.RecordInboundAsync(
            hubId,
            new RecordInboundRequest(
                sourceMarketId,
                deliveryScheduleId,
                [new RecordInboundItemRequest(marketProductId, null, 10m)],
                arrivedAt),
            default);

        result.Should().BeOfType<CreatedResult>();
        await sender.Received(1).Send(
            Arg.Is<RecordInboundCommand>(command =>
                command.HubId == hubId &&
                command.SourceMarketId == sourceMarketId &&
                command.DeliveryScheduleId == deliveryScheduleId &&
                command.ArrivedAt == arrivedAt &&
                command.Items.Count == 1 &&
                command.Items[0].MarketProductId == marketProductId &&
                command.Items[0].QuantityKg == 10m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanInboundAsync_NoMatch_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ScanInboundCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubInboundDto>.Failure(
                Error.Validation("SCAN_NO_MATCH", "Scan code did not match.")));
        var controller = new HubInboundController(sender);

        var result = await controller.ScanInboundAsync(new ScanInboundRequest("bad-code"), default);

        result.Should().BeOfType<NotFoundObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ScanInboundCommand>(command => command.Code == "bad-code"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPendingInboundAsync_Success_ReturnsPagedEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var page = new HubInboundPageDto([CreateDto(hubId)], 25, "next", 10m);
        sender.Send(Arg.Any<GetPendingInboundQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubInboundPageDto>.Success(page));
        var controller = new HubInboundController(sender);

        var result = await controller.GetPendingInboundAsync(hubId, "cursor", 25, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetPendingInboundQuery>(query =>
                query.HubId == hubId &&
                query.Cursor == "cursor" &&
                query.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListInboundAsync_Success_SendsQueryWithDateAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 10);
        var page = new HubInboundPageDto([CreateDto(hubId)], 50, null, 10m);
        sender.Send(Arg.Any<ListInboundQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubInboundPageDto>.Success(page));
        var controller = new HubInboundController(sender);

        var result = await controller.ListInboundAsync(hubId, date, null, 50, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListInboundQuery>(query =>
                query.HubId == hubId &&
                query.Date == date &&
                query.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    private static HubInboundDto CreateDto(Guid hubId) =>
        new(
            Guid.NewGuid(),
            hubId,
            null,
            null,
            null,
            [new HubInboundItemDto(Guid.NewGuid(), null, 10m)],
            10m,
            DateTime.UtcNow,
            null,
            null,
            HubInboundEvent.StatusPending,
            HubInboundEvent.ConditionOk,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
