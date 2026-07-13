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
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IOperationalSettingsRepository _operationalSettings = Substitute.For<IOperationalSettingsRepository>();

    private readonly ConfirmOrderCommandHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OtherRestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ConfirmOrderCommandHandlerTests()
    {
        _sut = new ConfirmOrderCommandHandler(_orderRepository, _restaurantReader, _creditService, _operationalSettings);

        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(OperationalSettings.CreateDefault());

        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

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

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundAsync()
    {
        _orderRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OrderBelongsToAnotherRestaurant_ReturnsForbiddenAsync()
    {
        var order = NewDraftOrderWithItem(OtherRestaurantId);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FORBIDDEN");
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

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

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

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

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

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

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

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Handle_Success_ConfirmsOrderChargesCreditAndPersistsAsync()
    {
        var order = NewDraftOrderWithItem();
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("confirmed");
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

        var firstResult = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);
        var secondResult = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default);

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

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default, confirmedAtUtc);

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
            .Returns(new OperationalSettings(new TimeOnly(21, 0), true, "hub_relay"));
        var order = NewDraftOrderWithItem(scheduledFor: null);
        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default, confirmedAtUtc);

        result.IsSuccess.Should().BeTrue();
        order.ScheduledFor.Should().Be(new DateTime(2026, 6, 19, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_ScheduledForBeyondDPlus7AtConfirmTime_ReturnsDeliveryDateOutOfWindowAsync()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));

        _orderRepository.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _sut.Handle(new ConfirmOrderCommand(UserId, order.Id), default, confirmedAtUtc);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
        order.Status.Should().Be(OrderStatus.Draft);
        await _creditService.DidNotReceive().ChargeAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
