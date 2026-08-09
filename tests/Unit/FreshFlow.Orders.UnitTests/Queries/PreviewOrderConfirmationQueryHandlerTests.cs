using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class PreviewOrderConfirmationQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IOperationalSettingsRepository _operationalSettings = Substitute.For<IOperationalSettingsRepository>();
    private readonly IRoadDistanceProvider _roadDistanceProvider = Substitute.For<IRoadDistanceProvider>();

    private readonly PreviewOrderConfirmationQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();
    private static readonly Guid DeliveryAddressId = Guid.NewGuid();

    public PreviewOrderConfirmationQueryHandlerTests()
    {
        _sut = new PreviewOrderConfirmationQueryHandler(
            _orderRepository, _restaurantReader, _marketProductReader, _creditService,
            _operationalSettings, _roadDistanceProvider);

        _roadDistanceProvider.GetDistanceAsync(
                Arg.Any<IReadOnlyList<GeoCoordinate>>(),
                Arg.Any<GeoCoordinate>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var origin = call.ArgAt<IReadOnlyList<GeoCoordinate>>(0)[0];
                return new RoadDistanceResult(11_120, 1_200, origin, false, "GOONG");
            });

        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(OperationalSettings.CreateDefault());

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        _creditService.CanChargeAsync(RestaurantId, Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditCheckDto>.Success(
                new CreditCheckDto(RestaurantId, 1_000m, 0m, 1_000m, 100_000m, CanCharge: true)));
    }

    private static Order NewDraftOrderWithItem(Guid? restaurantId = null, DateTime? scheduledFor = null)
    {
        var order = new Order(restaurantId ?? RestaurantId, scheduledFor, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        return order;
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderBelongsToAnotherRestaurant_ReturnsForbiddenAsync()
    {
        var order = NewDraftOrderWithItem(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_DraftOrderWithinWindow_ReturnsWouldSucceedWithNoIssuesAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeTrue();
        result.Value.Issues.Should().BeEmpty();
        result.Value.TotalAmount.Should().Be(100_000m);
        result.Value.RemainingCreditAfter.Should().Be(1_000m - 100_000m);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Handle_WithAddress_ReturnsSameCommercialTotalAsConfirmationAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryAddressSourceDto(
                DeliveryAddressId, "Bếp trưởng", "0901234567",
                "1 Test Street", 10.123456m, 106.123456m));
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(
                MarketProductId, "Cà chua", 20_000m, 100, 1, "8",
                10.023456m, 106.123456m));

        var result = await _sut.Handle(
            new PreviewOrderConfirmationQuery(UserId, order.Id, DeliveryAddressId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeTrue();
        result.Value.SubtotalAmount.Should().Be(100_000m);
        result.Value.VatAmount.Should().Be(8_000m);
        result.Value.DeliveryDistanceKm.Should().Be(11.12m);
        result.Value.DeliveryFee.Should().Be(55_600m);
        result.Value.DeliveryDurationSeconds.Should().Be(1_200);
        result.Value.RoutingProvider.Should().Be("GOONG");
        result.Value.TotalAmount.Should().Be(163_600m);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Handle_NonDraftOrder_ReturnsOrderNotDraftIssueAsync()
    {
        var order = NewDraftOrderWithItem();
        order.Confirm();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeFalse();
        result.Value.Issues.Should().ContainSingle(i => i.Code == "ORDER_NOT_DRAFT");
    }

    [Fact]
    public async Task Handle_EmptyOrder_ReturnsOrderEmptyIssueAsync()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeFalse();
        result.Value.Issues.Should().ContainSingle(i => i.Code == "ORDER_EMPTY");
    }

    [Fact]
    public async Task Handle_ScheduledForBeyondDPlus7_ReturnsDeliveryDateOutOfWindowIssueAsync()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new PreviewOrderConfirmationQuery(UserId, order.Id), default, confirmedAtUtc);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeFalse();
        result.Value.Issues.Should().ContainSingle(i => i.Code == "DELIVERY_DATE_OUT_OF_WINDOW");
    }

    [Fact]
    public async Task Handle_NonDraftAndOutOfWindow_ReturnsBothIssuesNotJustFirstAsync()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));
        order.Confirm();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(
            new PreviewOrderConfirmationQuery(UserId, order.Id), default, confirmedAtUtc);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeFalse();
        result.Value.Issues.Select(i => i.Code).Should().Contain("ORDER_NOT_DRAFT");
        result.Value.Issues.Select(i => i.Code).Should().Contain("DELIVERY_DATE_OUT_OF_WINDOW");
        result.Value.Issues.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_CreditLimitExceeded_ReturnsCreditIssueWithNullRemainingCreditAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _creditService.CanChargeAsync(RestaurantId, order.TotalAmount, Arg.Any<CancellationToken>())
            .Returns(Result<CreditCheckDto>.Failure(
                Error.Validation("CREDIT_LIMIT_EXCEEDED", "Requested amount exceeds available credit.")));

        var result = await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.WouldSucceed.Should().BeFalse();
        result.Value.Issues.Should().ContainSingle(i => i.Code == "CREDIT_LIMIT_EXCEEDED");
        result.Value.RemainingCreditAfter.Should().BeNull();
        result.Value.ResolvedScheduledFor.Should().BeNull();
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Handle_DoesNotMutateOrderOrChargeCreditAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        await _sut.Handle(new PreviewOrderConfirmationQuery(UserId, order.Id), default);

        order.Status.Should().Be(OrderStatus.Draft);
        _orderRepository.DidNotReceive().Track(Arg.Any<Order>());
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
