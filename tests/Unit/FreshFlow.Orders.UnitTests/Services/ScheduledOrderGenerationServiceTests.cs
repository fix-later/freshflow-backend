using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class ScheduledOrderGenerationServiceTests
{
    private readonly IScheduledOrderRepository _scheduledOrderRepository = Substitute.For<IScheduledOrderRepository>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly List<Order> _addedOrders = [];
    private readonly ScheduledOrderGenerationService _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();

    public ScheduledOrderGenerationServiceTests()
    {
        _orderRepository.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => _addedOrders.Add(call.Arg<Order>()));
        _scheduledOrderRepository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _sut = new ScheduledOrderGenerationService(_scheduledOrderRepository, _orderRepository);
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
        await _scheduledOrderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateDueAsync_DailyScheduleWithMissedExecutions_CreatesEveryDueInstanceAsync()
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
        _scheduledOrderRepository.Received(1).Track(schedule);
        await _scheduledOrderRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await _scheduledOrderRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ScheduledOrder NewSchedule(RecurrenceType recurrenceType, DateTime firstRunAt) =>
        new(RestaurantId, recurrenceType, firstRunAt, "recurring");
}
