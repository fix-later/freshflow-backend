using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Logistics.Application.Commands.DeactivateVehicle;
using FreshFlow.Logistics.Application.Commands.RegisterVehicle;
using FreshFlow.Logistics.Application.Commands.UpdateVehicle;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Queries.GetVehicle;
using FreshFlow.Logistics.Application.Queries.ListVehicles;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class VehiclesControllerTests
{
    [Fact]
    public void VehiclesController_AllowsHubStaffReadOnly_WritesNarrowedToAdminAndOps()
    {
        var classAttr = typeof(VehiclesController).GetCustomAttribute<AuthorizeAttribute>();
        classAttr.Should().NotBeNull();
        classAttr!.Roles.Should().Be("admin,operations_manager,hub_staff");

        var writes = new[]
        {
            nameof(VehiclesController.RegisterVehicleAsync),
            nameof(VehiclesController.UpdateVehicleAsync),
            nameof(VehiclesController.DeactivateVehicleAsync),
        };

        foreach (var write in writes)
        {
            var methodAttr = typeof(VehiclesController).GetMethod(write)!.GetCustomAttribute<AuthorizeAttribute>();
            methodAttr.Should().NotBeNull($"{write} must exclude hub_staff");
            methodAttr!.Roles.Should().Be("admin,operations_manager");
        }
    }

    [Fact]
    public async Task RegisterVehicleAsync_Success_UsesRegisteredByClaimAndReturnsCreatedAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var dto = CreateDto();
        sender.Send(Arg.Any<RegisterVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehicleDto>.Success(dto));
        var controller = new VehiclesController(sender)
        {
            ControllerContext = CreateControllerContext(userId)
        };

        var result = await controller.RegisterVehicleAsync(
            new RegisterVehicleRequest("ABC-123", 1200, "van"),
            default);

        result.Should().BeOfType<CreatedAtActionResult>();
        await sender.Received(1).Send(
            Arg.Is<RegisterVehicleCommand>(command =>
                command.PlateNumber == "ABC-123" &&
                command.CapacityKg == 1200 &&
                command.VehicleType == "van" &&
                command.RegisteredBy == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterVehicleAsync_Success_FallsBackToSubClaimAsync()
    {
        var sender = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var dto = CreateDto();
        sender.Send(Arg.Any<RegisterVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehicleDto>.Success(dto));
        var identity = new ClaimsIdentity(
            [new Claim("sub", userId.ToString())],
            authenticationType: "Test");
        var controller = new VehiclesController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            }
        };

        await controller.RegisterVehicleAsync(new RegisterVehicleRequest("ABC-123", 1200, "van"), default);

        await sender.Received(1).Send(
            Arg.Is<RegisterVehicleCommand>(command => command.RegisteredBy == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListVehiclesAsync_Success_ReturnsPagedEnvelopeAsync()
    {
        var sender = Substitute.For<ISender>();
        var page = new VehiclePageDto([CreateDto()], 25, "next");
        sender.Send(Arg.Any<ListVehiclesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehiclePageDto>.Success(page));
        var controller = new VehiclesController(sender)
        {
            ControllerContext = CreateControllerContext(Guid.NewGuid())
        };

        var result = await controller.ListVehiclesAsync("cursor", 25, true, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<ListVehiclesQuery>(query =>
                query.Cursor == "cursor" &&
                query.PageSize == 25 &&
                query.IsActive == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetVehicleAsync_NotFound_Returns404Async()
    {
        var sender = Substitute.For<ISender>();
        var vehicleId = Guid.NewGuid();
        sender.Send(Arg.Any<GetVehicleQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehicleDto>.Failure(Error.NotFound("VEHICLE", vehicleId)));
        var controller = new VehiclesController(sender);

        var result = await controller.GetVehicleAsync(vehicleId, default);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateVehicleAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var vehicleId = Guid.NewGuid();
        sender.Send(Arg.Any<UpdateVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehicleDto>.Success(CreateDto(vehicleId)));
        var controller = new VehiclesController(sender);

        var result = await controller.UpdateVehicleAsync(
            vehicleId,
            new UpdateVehicleRequest("XYZ-789", 2400, "truck"),
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<UpdateVehicleCommand>(command =>
                command.Id == vehicleId &&
                command.PlateNumber == "XYZ-789" &&
                command.CapacityKg == 2400 &&
                command.VehicleType == "truck"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateVehicleAsync_Success_SendsCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var vehicleId = Guid.NewGuid();
        sender.Send(Arg.Any<DeactivateVehicleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehicleDto>.Success(CreateDto(vehicleId)));
        var controller = new VehiclesController(sender);

        var result = await controller.DeactivateVehicleAsync(vehicleId, default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<DeactivateVehicleCommand>(command => command.Id == vehicleId),
            Arg.Any<CancellationToken>());
    }

    private static ControllerContext CreateControllerContext(Guid userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private static VehicleDto CreateDto(Guid? id = null) =>
        new(
            id ?? Guid.NewGuid(),
            "ABC-123",
            1200,
            "van",
            true,
            true,
            DateTime.UtcNow,
            DateTime.UtcNow);
}
