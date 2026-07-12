using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Logistics.UnitTests.TestDoubles;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateProofUploadSignatureCommandHandlerTests
{
    [Fact]
    public async Task Handle_OwnerDriver_ReturnsSignatureAndUsesProofFolderAsync()
    {
        var driverId = Guid.NewGuid();
        var route = AssignedRoute(driverId);
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var deliveries = new InMemoryDeliveryRepository();
        var routes = new InMemoryDeliveryRouteRepository();
        var signer = Substitute.For<ICloudinarySignatureService>();
        signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult("sig", 123, "key", "cloud", "freshflow/proof-of-delivery"));
        await deliveries.AddRangeAsync([delivery], default);
        await routes.AddAsync(route, default);
        var sut = new CreateProofUploadSignatureCommandHandler(deliveries, routes, signer);

        var result = await sut.Handle(new CreateProofUploadSignatureCommand(delivery.Id, driverId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Signature.Should().Be("sig");
        result.Value.Folder.Should().Be("freshflow/proof-of-delivery");
        signer.Received(1).Sign(Arg.Is<CloudinarySignatureRequest>(
            request => request.Folder == "freshflow/proof-of-delivery"));
    }

    [Fact]
    public async Task Handle_DeliveryMissing_ReturnsNotFoundAndDoesNotSignAsync()
    {
        var signer = Substitute.For<ICloudinarySignatureService>();
        var sut = new CreateProofUploadSignatureCommandHandler(
            new InMemoryDeliveryRepository(),
            new InMemoryDeliveryRouteRepository(),
            signer);

        var result = await sut.Handle(
            new CreateProofUploadSignatureCommand(Guid.NewGuid(), Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_NOT_FOUND");
        signer.DidNotReceive().Sign(Arg.Any<CloudinarySignatureRequest>());
    }

    [Fact]
    public async Task Handle_DifferentDriver_ReturnsForbiddenAndDoesNotSignAsync()
    {
        var route = AssignedRoute(Guid.NewGuid());
        var delivery = Delivery.Create(route.Id, Guid.NewGuid(), 1);
        var deliveries = new InMemoryDeliveryRepository();
        var routes = new InMemoryDeliveryRouteRepository();
        var signer = Substitute.For<ICloudinarySignatureService>();
        await deliveries.AddRangeAsync([delivery], default);
        await routes.AddAsync(route, default);
        var sut = new CreateProofUploadSignatureCommandHandler(deliveries, routes, signer);

        var result = await sut.Handle(
            new CreateProofUploadSignatureCommand(delivery.Id, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        signer.DidNotReceive().Sign(Arg.Any<CloudinarySignatureRequest>());
    }

    private static DeliveryRoute AssignedRoute(Guid driverId)
    {
        var route = DeliveryRoute.CreateDirect(
            new DateOnly(2026, 7, 12),
            [
                new RouteStop(0, StopEntityType.market, Guid.NewGuid(), "Market", 10.1m, 106.1m, null, null),
                new RouteStop(1, StopEntityType.restaurant, Guid.NewGuid(), "Restaurant", 10.2m, 106.2m, null, null)
            ],
            null);
        route.Select();
        route.ApplyOptimization(route.Stops, 12m, 30, 100m, OptimizationCriteria.cost);
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        return route;
    }
}
