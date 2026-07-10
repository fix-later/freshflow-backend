using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.ListDiscrepancies;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyControllerTests
{
    [Fact]
    public void AcknowledgeDiscrepancyAsync_RequiresAdminOrOperationsManagerAuthorization()
    {
        var attr = typeof(HubInboundController)
            .GetMethod(nameof(HubInboundController.AcknowledgeDiscrepancyAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public async Task RecordDiscrepancyAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var inboundId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        sender.Send(Arg.Any<RecordDiscrepancyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDiscrepancyDto>.Success(CreateDto(hubId, inboundId, orderItemId)));
        var controller = new HubInboundController(sender);

        var result = await controller.RecordDiscrepancyAsync(
            hubId,
            inboundId,
            new RecordDiscrepancyRequest(
                orderItemId,
                2m,
                HubDiscrepancy.ConditionMissing,
                "Missing case"),
            default);

        result.Should().BeOfType<CreatedResult>();
        await sender.Received(1).Send(
            Arg.Is<RecordDiscrepancyCommand>(command =>
                command.HubId == hubId &&
                command.InboundEventId == inboundId &&
                command.OrderItemId == orderItemId &&
                command.AffectedQuantity == 2m &&
                command.ConditionStatus == HubDiscrepancy.ConditionMissing &&
                command.Notes == "Missing case"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListDiscrepanciesAsync_Success_SendsQueryAndReturnsPagedEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var page = new HubDiscrepancyPageDto([CreateDto(hubId, Guid.NewGuid(), Guid.NewGuid())], 25, "next");
        sender.Send(Arg.Any<ListDiscrepanciesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDiscrepancyPageDto>.Success(page));
        var controller = new HubInboundController(sender);

        var result = await controller.ListDiscrepanciesAsync(
            hubId,
            HubDiscrepancy.StatusOpen,
            "cursor",
            25,
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListDiscrepanciesQuery>(query =>
                query.HubId == hubId &&
                query.Status == HubDiscrepancy.StatusOpen &&
                query.Cursor == "cursor" &&
                query.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcknowledgeDiscrepancyAsync_Success_SendsCommandWithAuthenticatedUserAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var discrepancyId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        sender.Send(Arg.Any<AcknowledgeDiscrepancyCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDiscrepancyDto>.Success(CreateDto(hubId, Guid.NewGuid(), Guid.NewGuid())));
        var controller = new HubInboundController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, adminUserId.ToString())]))
                }
            }
        };

        var result = await controller.AcknowledgeDiscrepancyAsync(hubId, discrepancyId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<AcknowledgeDiscrepancyCommand>(command =>
                command.HubId == hubId &&
                command.DiscrepancyId == discrepancyId &&
                command.AdminUserId == adminUserId),
            Arg.Any<CancellationToken>());
    }

    private static HubDiscrepancyDto CreateDto(Guid hubId, Guid inboundId, Guid orderItemId) =>
        new(
            Guid.NewGuid(),
            hubId,
            inboundId,
            Guid.NewGuid(),
            orderItemId,
            1m,
            HubDiscrepancy.ConditionMissing,
            null,
            HubDiscrepancy.StatusOpen,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
