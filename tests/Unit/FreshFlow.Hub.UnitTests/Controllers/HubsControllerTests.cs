using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.CreateHub;
using FreshFlow.Hub.Application.Commands.DeactivateHub;
using FreshFlow.Hub.Application.Commands.UpdateHub;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.GetHub;
using FreshFlow.Hub.Application.Queries.ListHubs;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubsControllerTests
{
    [Fact]
    public void HubsController_RequiresAdminOrOperationsManagerAuthorization()
    {
        var attr = typeof(HubsController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public async Task CreateHubAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var marketId = Guid.NewGuid();
        var managedBy = Guid.NewGuid();
        var dto = CreateDto(marketId: marketId, managedBy: managedBy);
        sender.Send(Arg.Any<CreateHubCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDto>.Success(dto));
        var controller = new HubsController(sender);

        var result = await controller.CreateHubAsync(
            new CreateHubRequest(marketId, "Main Hub", "123 Road", 10m, 106m, 1000, managedBy),
            default);

        result.Should().BeOfType<CreatedAtActionResult>();
        await sender.Received(1).Send(
            Arg.Is<CreateHubCommand>(command =>
                command.MarketId == marketId &&
                command.Name == "Main Hub" &&
                command.Address == "123 Road" &&
                command.Latitude == 10m &&
                command.Longitude == 106m &&
                command.CapacityKg == 1000 &&
                command.ManagedBy == managedBy),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListHubsAsync_Success_ReturnsPagedEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var page = new HubPageDto([CreateDto()], 25, "next");
        sender.Send(Arg.Any<ListHubsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubPageDto>.Success(page));
        var controller = new HubsController(sender);

        var result = await controller.ListHubsAsync("cursor", 25, true, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListHubsQuery>(query =>
                query.Cursor == "cursor" &&
                query.PageSize == 25 &&
                query.IsActive == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHubAsync_NotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        sender.Send(Arg.Any<GetHubQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDto>.Failure(Error.NotFound("HUB", hubId)));
        var controller = new HubsController(sender);

        var result = await controller.GetHubAsync(hubId, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateHubAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var managedBy = Guid.NewGuid();
        sender.Send(Arg.Any<UpdateHubCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDto>.Success(CreateDto(hubId)));
        var controller = new HubsController(sender);

        var result = await controller.UpdateHubAsync(
            hubId,
            new UpdateHubRequest("Updated Hub", "456 Road", 11m, 107m, 1500, managedBy),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<UpdateHubCommand>(command =>
                command.HubId == hubId &&
                command.Name == "Updated Hub" &&
                command.Address == "456 Road" &&
                command.Latitude == 11m &&
                command.Longitude == 107m &&
                command.CapacityKg == 1500 &&
                command.ManagedBy == managedBy),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateHubAsync_PendingInboundFailure_Returns409Async()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        sender.Send(Arg.Any<DeactivateHubCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<HubDto>.Failure(
                Error.Validation("HUB_HAS_PENDING_DELIVERIES", "Hub has pending inbound deliveries.")));
        var controller = new HubsController(sender);

        var result = await controller.DeactivateHubAsync(hubId, default);

        result.Should().BeOfType<ConflictObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<DeactivateHubCommand>(command => command.HubId == hubId),
            Arg.Any<CancellationToken>());
    }

    private static HubDto CreateDto(
        Guid? id = null,
        Guid? marketId = null,
        Guid? managedBy = null) =>
        new(
            id ?? Guid.NewGuid(),
            marketId ?? Guid.NewGuid(),
            "Main Hub",
            "123 Road",
            10m,
            106m,
            1000,
            0,
            1000,
            true,
            managedBy,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
