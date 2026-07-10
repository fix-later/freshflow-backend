using System.Reflection;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZoneById;
using FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZones;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class DeliveryZonesControllerTests
{
    [Fact]
    public void DeliveryZonesController_RequiresAdminOrOperationsManagerAuthorization()
    {
        var attr = typeof(DeliveryZonesController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin,operations_manager");
    }

    [Fact]
    public async Task CreateDeliveryZoneAsync_Success_SendsCommandAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var dto = CreateDto();
        sender.Send(Arg.Any<CreateDeliveryZoneCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeliveryZoneDto>.Success(dto));
        var controller = new DeliveryZonesController(sender);

        var result = await controller.CreateDeliveryZoneAsync(
            new CreateDeliveryZoneRequest("district_1", "District 1", "Central"),
            default);

        result.Should().BeOfType<CreatedAtActionResult>();
        await sender.Received(1).Send(
            Arg.Is<CreateDeliveryZoneCommand>(command =>
                command.Code == "district_1" &&
                command.Name == "District 1" &&
                command.Description == "Central"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateDeliveryZoneAsync_DuplicateCode_Returns409Async()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CreateDeliveryZoneCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeliveryZoneDto>.Failure(
                Error.Conflict("DELIVERY_ZONE_CODE_EXISTS", "duplicate")));
        var controller = new DeliveryZonesController(sender);

        var result = await controller.CreateDeliveryZoneAsync(
            new CreateDeliveryZoneRequest("DISTRICT_1", "District 1", null),
            default);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ListDeliveryZonesAsync_Success_SendsQueryAsync()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetDeliveryZonesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DeliveryZoneDto>>.Success([CreateDto()]));
        var controller = new DeliveryZonesController(sender);

        var result = await controller.ListDeliveryZonesAsync(activeOnly: false, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<GetDeliveryZonesQuery>(query => !query.ActiveOnly),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDeliveryZoneAsync_NotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var zoneId = Guid.NewGuid();
        sender.Send(Arg.Any<GetDeliveryZoneByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeliveryZoneDto>.Failure(Error.NotFound("DELIVERY_ZONE", zoneId)));
        var controller = new DeliveryZonesController(sender);

        var result = await controller.GetDeliveryZoneAsync(zoneId, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateDeliveryZoneAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var zoneId = Guid.NewGuid();
        sender.Send(Arg.Any<UpdateDeliveryZoneCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeliveryZoneDto>.Success(CreateDto(zoneId)));
        var controller = new DeliveryZonesController(sender);

        var result = await controller.UpdateDeliveryZoneAsync(
            zoneId,
            new UpdateDeliveryZoneRequest("District One", "Updated"),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<UpdateDeliveryZoneCommand>(command =>
                command.Id == zoneId &&
                command.Name == "District One" &&
                command.Description == "Updated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateDeliveryZoneAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var zoneId = Guid.NewGuid();
        sender.Send(Arg.Any<DeactivateDeliveryZoneCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<DeliveryZoneDto>.Success(CreateDto(zoneId)));
        var controller = new DeliveryZonesController(sender);

        var result = await controller.DeactivateDeliveryZoneAsync(zoneId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<DeactivateDeliveryZoneCommand>(command => command.Id == zoneId),
            Arg.Any<CancellationToken>());
    }

    private static DeliveryZoneDto CreateDto(Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            "DISTRICT_1",
            "District 1",
            "Central",
            true,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
