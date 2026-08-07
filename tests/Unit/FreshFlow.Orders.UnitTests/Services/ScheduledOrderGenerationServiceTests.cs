using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class ScheduledOrderGenerationServiceTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IMarketProductReader _marketProductReader = Substitute.For<IMarketProductReader>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IOperationalSettingsRepository _operationalSettings = Substitute.For<IOperationalSettingsRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly List<Order> _addedOrders = [];
    private readonly ScheduledOrderGenerationService _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid DeliveryAddressId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public ScheduledOrderGenerationServiceTests()
    {
        _orderRepository.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _orderRepository.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => _addedOrders.Add(call.Arg<Order>()));
        _orderRepository.TryReserveStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _operationalSettings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(OperationalSettings.CreateDefault());
        _marketProductReader.FindAsync(MarketProductId, Arg.Any<CancellationToken>())
            .Returns(new MarketProductSnapshotDto(
                MarketProductId, "Cà chua", 20_000m, 100,
                OriginLatitude: 10.123456m, OriginLongitude: 106.123456m));
        _restaurantReader.FindByIdAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _restaurantReader.FindDeliveryAddressAsync(
                DeliveryAddressId, RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new DeliveryAddressSourceDto(
                DeliveryAddressId, "Bếp trưởng", "0901234567", "1 Test Street", 10.123456m, 106.123456m));
        _creditService.CanChargeAsync(RestaurantId, Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditCheckDto>.Success(
                new CreditCheckDto(RestaurantId, 1_000m, 0m, 1_000m, 100_000m, CanCharge: true)));
        _creditService.ChargeAsync(
                RestaurantId, Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<RestaurantCreditDto>.Success(
                new RestaurantCreditDto(RestaurantId, 1_000m, 100_000m, 900m, DateTime.UtcNow)));

        var orderConfirmationService = new OrderConfirmationService(
            _orderRepository, _restaurantReader, _marketProductReader, _creditService, _operationalSettings);
        _sut = new ScheduledOrderGenerationService(
            _scheduledOrderRepository, _orderRepository, _restaurantReader, _marketProductReader,
            orderConfirmationService, _publisher);
    }

    [Fact]
    public async Task GenerateDueAsync_NoDueSchedules_DoesNotPersistAsync()
    {
        var now = new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc);
        var schedule = NewSchedule(RecurrenceType.Daily, now.AddDays(1));
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var result = await _sut.GenerateDueAsync(now, default);

        result.CreatedOrderCount.Should().Be(0);
        result.MissedExecutionCount.Should().Be(0);
        _addedOrders.Should().BeEmpty();
        await _orderRepository.DidNotReceive().ExecuteInSerializableTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_LegacyScheduleWithNoItems_CreatesEmptyDraftsWithoutNotifyingAsync()
    {
        var firstRun = new DateTime(2026, 6, 16, 4, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 6, 18, 5, 0, 0, DateTimeKind.Utc);
        var schedule = NewSchedule(RecurrenceType.Daily, firstRun);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var result = await _sut.GenerateDueAsync(now, default);

        result.CreatedOrderCount.Should().Be(3);
        result.MissedExecutionCount.Should().Be(2);
        _addedOrders.Select(o => o.ScheduledFor).Should().Equal(
            firstRun,
            firstRun.AddDays(1),
            firstRun.AddDays(2));
        _addedOrders.Should().OnlyContain(o =>
            o.RestaurantId == RestaurantId
            && o.ScheduledOrderId == schedule.Id
            && o.Status == OrderStatus.Draft
            && o.Items.Count == 0);
        schedule.LastExecutedAt.Should().Be(firstRun.AddDays(2));
        _scheduledOrderRepository.Received(3).Track(schedule);
        await _publisher.DidNotReceive().Publish(
            Arg.Any<ScheduledOrderNeedsAttentionIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_WeeklySchedule_UsesLastExecutedAtForNextOccurrenceAsync()
    {
        var firstRun = new DateTime(2026, 6, 1, 4, 0, 0, DateTimeKind.Utc);
        var lastExecuted = firstRun.AddDays(7);
        var now = firstRun.AddDays(14).AddMinutes(1);
        var schedule = NewSchedule(RecurrenceType.Weekly, firstRun);
        schedule.RecordExecution(lastExecuted);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var result = await _sut.GenerateDueAsync(now, default);

        result.CreatedOrderCount.Should().Be(1);
        result.MissedExecutionCount.Should().Be(0);
        _addedOrders.Single().ScheduledFor.Should().Be(firstRun.AddDays(14));
        schedule.LastExecutedAt.Should().Be(firstRun.AddDays(14));
    }

    [Fact]
    public async Task GenerateDueAsync_RepeatedRunForSameWindow_DoesNotCreateDuplicateInstancesAsync()
    {
        var now = new DateTime(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        var schedule = NewSchedule(RecurrenceType.Daily, now);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var firstResult = await _sut.GenerateDueAsync(now, default);
        _scheduledOrderRepository.ClearReceivedCalls();
        var secondResult = await _sut.GenerateDueAsync(now, default);

        firstResult.CreatedOrderCount.Should().Be(1);
        secondResult.CreatedOrderCount.Should().Be(0);
        _addedOrders.Should().ContainSingle();
        await _orderRepository.Received(1).ExecuteInSerializableTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_ScheduleWithItemsAndAddress_AutoConfirmsOrderAsync()
    {
        var firstRun = new DateTime(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        var schedule = NewScheduleWithTemplate(firstRun);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var result = await _sut.GenerateDueAsync(firstRun, default);

        result.CreatedOrderCount.Should().Be(1);
        var order = _addedOrders.Single();
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.Items.Should().ContainSingle(i => i.MarketProductId == MarketProductId && i.Quantity == 5);
        await _creditService.Received(1).ChargeAsync(
            RestaurantId, order.Id, Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().Publish(
            Arg.Any<ScheduledOrderNeedsAttentionIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_CreditLimitExceeded_DegradesToDraftAndNotifiesAsync()
    {
        var firstRun = new DateTime(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        var schedule = NewScheduleWithTemplate(firstRun);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);
        _creditService.CanChargeAsync(RestaurantId, Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditCheckDto>.Failure(
                Error.Validation("CREDIT_LIMIT_EXCEEDED", "Requested amount exceeds available credit.")));

        var result = await _sut.GenerateDueAsync(firstRun, default);

        result.CreatedOrderCount.Should().Be(1);
        var order = _addedOrders.Single();
        order.Status.Should().Be(OrderStatus.Draft);
        schedule.LastExecutedAt.Should().Be(firstRun);
        await _publisher.Received(1).Publish(
            Arg.Is<ScheduledOrderNeedsAttentionIntegrationEvent>(e =>
                e.ScheduledOrderId == schedule.Id
                && e.OrderId == order.Id
                && e.RestaurantId == RestaurantId
                && e.Reason.Contains("credit", StringComparison.OrdinalIgnoreCase)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_InsufficientStock_DegradesToDraftAndNotifiesAsync()
    {
        var firstRun = new DateTime(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        var schedule = NewScheduleWithTemplate(firstRun);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);
        _orderRepository.TryReserveStockAsync(
                Arg.Any<IReadOnlyList<StockReservation>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.GenerateDueAsync(firstRun, default);

        result.CreatedOrderCount.Should().Be(1);
        _addedOrders.Single().Status.Should().Be(OrderStatus.Draft);
        await _publisher.Received(1).Publish(
            Arg.Any<ScheduledOrderNeedsAttentionIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_RestaurantNotApproved_DegradesToDraftAndNotifiesAsync()
    {
        var firstRun = new DateTime(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        var schedule = NewScheduleWithTemplate(firstRun);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);
        _restaurantReader.FindByIdAsync(RestaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: false));

        var result = await _sut.GenerateDueAsync(firstRun, default);

        result.CreatedOrderCount.Should().Be(1);
        _addedOrders.Single().Status.Should().Be(OrderStatus.Draft);
        await _creditService.DidNotReceive().CanChargeAsync(
            RestaurantId, Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(
            Arg.Any<ScheduledOrderNeedsAttentionIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_MissingDeliveryAddress_DegradesToDraftAndNotifiesAsync()
    {
        var firstRun = new DateTime(2026, 6, 18, 4, 0, 0, DateTimeKind.Utc);
        var schedule = new ScheduledOrder(RestaurantId, RecurrenceType.Daily, firstRun, null);
        schedule.AddItem(MarketProductId, 5);
        _scheduledOrderRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var result = await _sut.GenerateDueAsync(firstRun, default);

        result.CreatedOrderCount.Should().Be(1);
        _addedOrders.Single().Status.Should().Be(OrderStatus.Draft);
        await _publisher.Received(1).Publish(
            Arg.Any<ScheduledOrderNeedsAttentionIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    private static ScheduledOrder NewSchedule(RecurrenceType recurrenceType, DateTime firstRunAt) =>
        new(RestaurantId, recurrenceType, firstRunAt, "recurring");

    private static ScheduledOrder NewScheduleWithTemplate(DateTime firstRunAt)
    {
        var schedule = new ScheduledOrder(
            RestaurantId, RecurrenceType.Daily, firstRunAt, "recurring", DeliveryAddressId);
        schedule.AddItem(MarketProductId, 5);
        return schedule;
    }
}
