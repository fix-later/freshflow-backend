using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.API.Extensions;
using FreshFlow.Logistics.Application.Commands.AssignVehicleToHub;
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
using Microsoft.AspNetCore.Mvc.Routing;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class VehiclesControllerTests
{
    [Theory]
    [InlineData("VEHICLE_HUB_UNASSIGNED")]
    [InlineData("VEHICLE_HUB_MISMATCH")]
    public void VehicleHubValidationErrors_Return400(string code)
    {
        var result = Error.Validation(code, "bad request").ToActionResult();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void HubNotFoundError_Returns404()
    {
        var result = Error.NotFound("HUB", Guid.NewGuid()).ToActionResult();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void VehiclesController_EveryWriteExcludesHubStaff()
    {
        var classAttr = typeof(VehiclesController).GetCustomAttribute<AuthorizeAttribute>();
        classAttr.Should().NotBeNull();
        classAttr!.Roles.Should().Be("admin,operations_manager,hub_staff");

        foreach (var write in WriteActions(typeof(VehiclesController)))
        {
            var methodAttr = write.GetCustomAttribute<AuthorizeAttribute>();
            methodAttr.Should().NotBeNull($"{write.Name} is a write action and must exclude hub_staff");
            methodAttr!.Roles.Should().Be("admin,operations_manager", $"{write.Name} must exclude hub_staff");
        }
    }

    // Reflects over every public action mapped to a mutating HTTP verb, so a future write endpoint added
    // without a narrowing [Authorize] fails this test instead of silently inheriting the hub_staff class gate.
    private static IEnumerable<MethodInfo> WriteActions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(a => a.HttpMethods)
                .Any(verb => verb is "POST" or "PUT" or "PATCH" or "DELETE"));

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

        var result = await controller.ListVehiclesAsync("cursor", 25, true, null, default);

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

    [Fact]
    public async Task AssignHubAsync_Success_SendsCommandThroughSenderAsync()
    {
        var sender = Substitute.For<ISender>();
        var vehicleId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        sender.Send(Arg.Any<AssignVehicleToHubCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<VehicleDto>.Success(CreateDto(vehicleId)));
        var controller = new VehiclesController(sender);

        var result = await controller.AssignHubAsync(
            vehicleId, new AssignVehicleToHubRequest(hubId), default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<AssignVehicleToHubCommand>(command =>
                command.VehicleId == vehicleId && command.HubId == hubId),
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
