using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetOrder;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetOrderQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductImageReader _marketProductImageReader =
        Substitute.For<IMarketProductImageReader>();
    private readonly IDeliveryProofReader _deliveryProofReader = Substitute.For<IDeliveryProofReader>();
    private readonly GetOrderQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public GetOrderQueryHandlerTests()
    {
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _sut = new GetOrderQueryHandler(
            _orderRepository,
            _restaurantReader,
            _marketProductImageReader,
            _deliveryProofReader);
    }

    [Fact]
    public async Task Handle_OrderMissing_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_RestaurantUserRequestingAnotherRestaurantsOrder_ReturnsForbiddenAsync()
    {
        var order = NewOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _marketProductImageReader.DidNotReceive()
            .ReadImagesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _deliveryProofReader.DidNotReceive()
            .FindByOrderIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OwnerRestaurant_ReturnsOrderDetailAsync()
    {
        var order = NewOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(order.Id);
        result.Value.RestaurantId.Should().Be(RestaurantId);
        result.Value.Status.Should().Be("draft");
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ImageUrl.Should().BeNull();
        result.Value.ProofUrl.Should().BeNull();
        result.Value.CreatedAt.Should().Be(order.CreatedAt);
        result.Value.UpdatedAt.Should().Be(order.UpdatedAt);
        await _deliveryProofReader.DidNotReceive()
            .FindByOrderIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeliveredOrder_ReturnsProofUrlAsync()
    {
        var order = NewOrder(RestaurantId);
        foreach (var status in new[]
                 {
                     OrderStatus.Confirmed,
                     OrderStatus.Batched,
                     OrderStatus.PickedUp,
                     OrderStatus.AtHub,
                     OrderStatus.Delivering,
                     OrderStatus.Delivered
                 })
            order.AdvanceStatus(status).IsSuccess.Should().BeTrue();

        const string proofUrl = "https://res.cloudinary.com/demo/image/upload/pod.jpg";
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _deliveryProofReader.FindByOrderIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(proofUrl);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: false, order.Id), default);

        result.Value.Status.Should().Be("delivered");
        result.Value.ProofUrl.Should().Be(proofUrl);
    }

    [Fact]
    public async Task Handle_MapsAvailableItemImagesAsync()
    {
        var order = NewOrder(RestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductImageReader.ReadImagesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>
            {
                [MarketProductId] = "https://img/tomato.jpg"
            });

        var result = await _sut.Handle(
            new GetOrderQuery(UserId, IsAdmin: false, order.Id),
            default);

        result.Value.Items.Should().ContainSingle()
            .Which.ImageUrl.Should().Be("https://img/tomato.jpg");
    }

    [Fact]
    public async Task Handle_AdminCanReadAnyOrderWithoutOwnershipLookupAsync()
    {
        var order = NewOrder(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new GetOrderQuery(UserId, IsAdmin: true, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RestaurantId.Should().Be(OtherRestaurantId);
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    private static Order NewOrder(Guid restaurantId)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: "Giao sớm");
        order.AddItem(MarketProductId, "Cà chua", 3, 20_000m);
        return order;
    }
}
