using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.CreateClaimProofUploadSignature;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateClaimProofUploadSignatureCommandHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurants = Substitute.For<IRestaurantReader>();
    private readonly ICloudinarySignatureService _signer = Substitute.For<ICloudinarySignatureService>();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public CreateClaimProofUploadSignatureCommandHandlerTests()
    {
        _restaurants.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
    }

    [Fact]
    public async Task Handle_OwnerOrder_ReturnsSignedUploadAsync()
    {
        var order = new Order(RestaurantId, null, null);
        order.AddItem(Guid.NewGuid(), "Tomato", 1, 100_000m);
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult("sig", 123L, "key", "cloud", "freshflow/order-claims"));
        var sut = new CreateClaimProofUploadSignatureCommandHandler(_orders, _restaurants, _signer);

        var result = await sut.Handle(new CreateClaimProofUploadSignatureCommand(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Folder.Should().Be("freshflow/order-claims");
        _signer.Received(1).Sign(Arg.Is<CloudinarySignatureRequest>(r => r.Folder == "freshflow/order-claims"));
    }

    [Fact]
    public async Task Handle_OtherRestaurantsOrder_ReturnsForbiddenAsync()
    {
        var order = new Order(Guid.NewGuid(), null, null);
        order.AddItem(Guid.NewGuid(), "Tomato", 1, 100_000m);
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new CreateClaimProofUploadSignatureCommandHandler(_orders, _restaurants, _signer);

        var result = await sut.Handle(new CreateClaimProofUploadSignatureCommand(UserId, order.Id), default);

        result.Error.Code.Should().Be("FORBIDDEN");
        _signer.DidNotReceive().Sign(Arg.Any<CloudinarySignatureRequest>());
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        var orderId = Guid.NewGuid();
        _orders.FindByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns((Order?)null);
        var sut = new CreateClaimProofUploadSignatureCommandHandler(_orders, _restaurants, _signer);

        var result = await sut.Handle(new CreateClaimProofUploadSignatureCommand(UserId, orderId), default);

        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }
}
