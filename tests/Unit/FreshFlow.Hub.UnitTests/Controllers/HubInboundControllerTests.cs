using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.CreateCrossDock;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.RecordOutbound;
using FreshFlow.Hub.Application.Commands.ScanInbound;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.ListCrossDock;
using FreshFlow.Hub.Application.Queries.ListInbound;
using FreshFlow.Hub.Application.Queries.ListOutbound;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        var actorUserId = Guid.NewGuid();
        sender.Send(Arg.Any<RecordInboundCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubInboundDto>.Success(CreateDto(hubId)));
        var controller = CreateController(sender, actorUserId);

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
                command.ActorUserId == actorUserId &&
                !command.BypassHubAssignment &&
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
        var controller = CreateController(sender);

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
        var actorUserId = Guid.NewGuid();
        var page = new HubInboundPageDto([CreateDto(hubId)], 25, "next", 10m);
        sender.Send(Arg.Any<GetPendingInboundQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubInboundPageDto>.Success(page));
        var controller = CreateController(sender, actorUserId, "operations_manager");

        var result = await controller.GetPendingInboundAsync(hubId, "cursor", 25, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetPendingInboundQuery>(query =>
                query.HubId == hubId &&
                query.Cursor == "cursor" &&
                query.PageSize == 25 &&
                query.ActorUserId == actorUserId &&
                query.BypassHubAssignment),
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
        var controller = CreateController(sender);

        var result = await controller.ListInboundAsync(hubId, date, null, 50, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListInboundQuery>(query =>
                query.HubId == hubId &&
                query.Date == date &&
                query.PageSize == 50),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCrossDockAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var inboundId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        sender.Send(Arg.Any<CreateCrossDockCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<CrossDockTransferDto>.Success(CreateCrossDockDto(hubId, inboundId, routeId)));
        var controller = CreateController(sender);

        var result = await controller.CreateCrossDockAsync(
            hubId,
            new CreateCrossDockRequest(inboundId, routeId, "Dock 1"),
            default);

        result.Should().BeOfType<CreatedResult>();
        await sender.Received(1).Send(
            Arg.Is<CreateCrossDockCommand>(command =>
                command.HubId == hubId &&
                command.InboundEventId == inboundId &&
                command.OutboundRouteId == routeId &&
                command.Notes == "Dock 1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCrossDockAsync_Success_SendsQueryAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        sender.Send(Arg.Any<ListCrossDockQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CrossDockTransferPageDto>.Success(
                new CrossDockTransferPageDto([CreateCrossDockDto(hubId, Guid.NewGuid(), Guid.NewGuid())], 25, null)));
        var controller = CreateController(sender);

        var result = await controller.ListCrossDockAsync(hubId, "pending", "cursor", 25, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListCrossDockQuery>(query =>
                query.HubId == hubId &&
                query.Status == "pending" &&
                query.Cursor == "cursor" &&
                query.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordOutboundAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var marketProductId = Guid.NewGuid();
        var dispatchedAt = DateTime.UtcNow;
        sender.Send(Arg.Any<RecordOutboundCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubOutboundEventDto>.Success(CreateOutboundDto(hubId, routeId)));
        var controller = CreateController(sender);

        var result = await controller.RecordOutboundAsync(
            hubId,
            new RecordOutboundRequest(
                routeId,
                [new RecordOutboundItemRequest(marketProductId, null, 3m)],
                dispatchedAt),
            default);

        result.Should().BeOfType<CreatedResult>();
        await sender.Received(1).Send(
            Arg.Is<RecordOutboundCommand>(command =>
                command.HubId == hubId &&
                command.DestinationRouteId == routeId &&
                command.DispatchedAt == dispatchedAt &&
                command.Items.Count == 1 &&
                command.Items[0].MarketProductId == marketProductId &&
                command.Items[0].QuantityKg == 3m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListOutboundAsync_Success_SendsQueryWithDateAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 11);
        sender.Send(Arg.Any<ListOutboundQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubOutboundPageDto>.Success(
                new HubOutboundPageDto([CreateOutboundDto(hubId, Guid.NewGuid())], 50, null, 10m)));
        var controller = CreateController(sender);

        var result = await controller.ListOutboundAsync(hubId, date, null, 50, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListOutboundQuery>(query =>
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

    private static HubInboundController CreateController(
        ISender sender,
        Guid? userId = null,
        string? role = null) =>
        new(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        role is null
                            ? [new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString())]
                            :
                            [
                                new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString()),
                                new Claim(ClaimTypes.Role, role)
                            ],
                        "Test"))
                }
            }
        };

    private static CrossDockTransferDto CreateCrossDockDto(Guid hubId, Guid inboundId, Guid routeId) =>
        new(
            Guid.NewGuid(),
            hubId,
            inboundId,
            routeId,
            CrossDockTransfer.StatusPending,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);

    private static HubOutboundEventDto CreateOutboundDto(Guid hubId, Guid routeId) =>
        new(
            Guid.NewGuid(),
            hubId,
            routeId,
            [new HubOutboundItemDto(Guid.NewGuid(), null, 10m)],
            10m,
            DateTime.UtcNow,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
