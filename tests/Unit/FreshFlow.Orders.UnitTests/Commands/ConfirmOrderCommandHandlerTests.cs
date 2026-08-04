using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.ConfirmOrder;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ConfirmOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IOperationalSettingsRepository _operationalSettings = Substitute.For<IOperationalSettingsRepository>();

    private readonly ConfirmOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();
    private static readonly Guid DeliveryAddressId = Guid.NewGuid();

    public ConfirmOrderCommandHandlerTests()
    {
        _sut = new ConfirmOrderCommandHandler(
            _orderRepository, _restaurantReader, _marketProductReader, _creditService, _operationalSettings);

        _orderRepository.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _orderRepository.TryReserveStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(OperationalSettings.CreateDefault());
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(
                MarketProductId, "Cà chua", 20_000m, 100,
                OriginLatitude: 10.123456m, OriginLongitude: 106.123456m));

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryAddressSourceDto(
                DeliveryAddressId, "Bếp trưởng", "0901234567",
                "1 Test Street", 10.123456m, 106.123456m));

        _creditService.CanChargeAsync(RestaurantId, Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditCheckDto>.Success(
                new CreditCheckDto(RestaurantId, 1_000m, 0m, 1_000m, 100_000m, CanCharge: true)));

        _creditService.ChargeAsync(
                RestaurantId, Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<RestaurantCreditDto>.Success(
                new RestaurantCreditDto(RestaurantId, 1_000m, 100_000m, 900m, DateTime.UtcNow)));
    }

    private static Order NewDraftOrderWithItem(Guid? restaurantId = null, DateTime? scheduledFor = null)
    {
        var order = new Order(restaurantId ?? RestaurantId, scheduledFor, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        return order;
    }

    private static ConfirmOrderCommand Command(Guid orderId) =>
        new(UserId, orderId, DeliveryAddressId);

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(Command(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderBelongsToAnotherRestaurant_ReturnsForbiddenAsync()
    {
        var order = NewDraftOrderWithItem(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
        await _creditService.DidNotReceive().CanChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuspendedRestaurant_ReturnsNotActiveWithoutChargingAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        // Restaurant was suspended after the draft was created → IsApproved is now false.
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: false));

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_ACTIVE");
        order.Status.Should().Be(OrderStatus.Draft);
        await _creditService.DidNotReceive().CanChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeliveryAddressNotFound_ReturnsNotFoundWithoutChargingAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
        await _creditService.DidNotReceive().CanChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreditLimitExceeded_ReturnsCreditErrorWithoutConfirmingAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _creditService.CanChargeAsync(RestaurantId, order.TotalAmount, Arg.Any<CancellationToken>())
            .Returns(Result<CreditCheckDto>.Failure(
                Error.Validation("CREDIT_LIMIT_EXCEEDED", "Requested amount exceeds available credit.")));

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_LIMIT_EXCEEDED");
        order.Status.Should().Be(OrderStatus.Draft);
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyOrder_ReturnsOrderEmptyErrorAsync()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_EMPTY");
        await _creditService.DidNotReceive().CanChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonDraftOrder_ReturnsOrderNotDraftBeforeCreditCheckAsync()
    {
        var order = NewDraftOrderWithItem();
        order.Confirm();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
        await _creditService.DidNotReceive().CanChargeAsync(
            Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ChargeFails_ReturnsChargeErrorWithoutPersistingAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _creditService.ChargeAsync(
                RestaurantId, order.Id, order.TotalAmount, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<RestaurantCreditDto>.Failure(
                Error.Conflict("OPTIMISTIC_CONCURRENCY_CONFLICT", "conflict")));

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Handle_ReservationFails_ReturnsInsufficientStockWithoutChargingAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _orderRepository.TryReserveStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INSUFFICIENT_STOCK");
        order.Status.Should().Be(OrderStatus.Draft);
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BelowMoq_DoesNotReserveOrChargeAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(
                MarketProductId, "Cà chua", 20_000m, 100, 6, "5",
                10.123456m, 106.123456m));

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MINIMUM_ORDER_QUANTITY_NOT_MET");
        order.Status.Should().Be(OrderStatus.Draft);
        await _orderRepository.DidNotReceive().TryReserveStockAsync(
            Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>());
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_SnapshotsVatAndChargesFinalTotalAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(
                MarketProductId, "Cà chua", 20_000m, 100, 1, "8",
                10.023456m, 106.123456m));

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.SubtotalAmount.Should().Be(100_000m);
        result.Value.VatAmount.Should().Be(8_000m);
        result.Value.DeliveryDistanceKm.Should().Be(11.12m);
        result.Value.DeliveryFee.Should().Be(55_600m);
        result.Value.TotalAmount.Should().Be(163_600m);
        order.Items.Single().VatRateCode.Should().Be("8");
        order.Items.Single().LockedVatAmount.Should().Be(8_000m);
        await _creditService.Received(1).ChargeAsync(
            RestaurantId, order.Id, 163_600m, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Success_ConfirmsOrderChargesCreditAndPersistsAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("confirmed");
        result.Value.DeliveryAddress.Should().BeEquivalentTo(new DeliveryAddressSnapshotDto(
            DeliveryAddressId, "Bếp trưởng", "0901234567",
            "1 Test Street", 10.123456m, 106.123456m));
        order.Status.Should().Be(OrderStatus.Confirmed);
        _orderRepository.Received(1).Track(order);
        await _creditService.Received(1).ChargeAsync(
            RestaurantId, order.Id, 100_000m, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CalledTwiceForSameOrder_ChargesCreditOnlyOnceAsync()
    {
        // Simulates a repeat-confirm request (e.g. a retried HTTP call) against the same
        // order. The order instance is reused across both calls, exactly as a shared,
        // request-scoped DbContext would return the now-Confirmed entity on the second load.
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var firstResult = await _sut.Handle(Command(order.Id), default);
        var secondResult = await _sut.Handle(Command(order.Id), default);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsFailure.Should().BeTrue();
        secondResult.Error.Code.Should().Be("ORDER_NOT_DRAFT");
        order.Status.Should().Be(OrderStatus.Confirmed);
        await _creditService.Received(1).ChargeAsync(
            RestaurantId, order.Id, Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PastCutoff_ReschedulesToNextDeliveryCycleBeforeConfirmingAsync()
    {
        // Confirm at 23:00 Vietnam time (16:00 UTC) — well past the 22:00 cutoff.
        var confirmedAtUtc = new DateTime(2026, 6, 18, 16, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: null);

        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default, confirmedAtUtc);

        result.IsSuccess.Should().BeTrue();
        order.ScheduledFor.Should().NotBeNull();
        order.ScheduledFor!.Value.Should().BeAfter(confirmedAtUtc);
    }

    [Fact]
    public async Task Handle_CustomCutoffFromSettings_UsesConfiguredCutoffAsync()
    {
        // 21:59 Vietnam time (14:59 UTC) is before the 22:00 default cutoff but past a
        // configured 21:00 cutoff — proves the handler reads operational_settings, not the
        // hardcoded default.
        var confirmedAtUtc = new DateTime(2026, 6, 18, 14, 59, 0, DateTimeKind.Utc);
        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new OperationalSettings(new TimeOnly(21, 0), true, "hub_relay", 7));
        var order = NewDraftOrderWithItem(scheduledFor: null);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default, confirmedAtUtc);

        result.IsSuccess.Should().BeTrue();
        order.ScheduledFor.Should().Be(new DateTime(2026, 6, 19, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_ScheduledForBeyondDPlus7AtConfirmTime_ReturnsDeliveryDateOutOfWindowAsync()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));

        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(Command(order.Id), default, confirmedAtUtc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
        order.Status.Should().Be(OrderStatus.Draft);
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
