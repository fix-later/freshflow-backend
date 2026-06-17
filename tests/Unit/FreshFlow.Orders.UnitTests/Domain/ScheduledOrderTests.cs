using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ScheduledOrderTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly DateTime FirstRunAt = DateTime.UtcNow.AddDays(1);

    [Fact]
    public void Constructor_ValidArgs_SetsAllProperties()
    {
        // Act
        var scheduledOrder = new ScheduledOrder(
            RestaurantId, RecurrenceType.Daily, FirstRunAt, notes: "Giao hàng tuần");

        // Assert
        scheduledOrder.RestaurantId.Should().Be(RestaurantId);
        scheduledOrder.RecurrenceType.Should().Be(RecurrenceType.Daily);
        scheduledOrder.FirstRunAt.Should().Be(FirstRunAt);
        scheduledOrder.Notes.Should().Be("Giao hàng tuần");
        scheduledOrder.LastExecutedAt.Should().BeNull();
        scheduledOrder.CancelledAt.Should().BeNull();
        scheduledOrder.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecordExecution_SetsLastExecutedAt()
    {
        // Arrange
        var scheduledOrder = new ScheduledOrder(RestaurantId, RecurrenceType.Weekly, FirstRunAt, null);
        var executedAt = DateTime.UtcNow;

        // Act
        scheduledOrder.RecordExecution(executedAt);

        // Assert
        scheduledOrder.LastExecutedAt.Should().Be(executedAt);
    }

    [Fact]
    public void Cancel_SetsCancelledAtAndIsActiveFalse()
    {
        // Arrange
        var scheduledOrder = new ScheduledOrder(RestaurantId, RecurrenceType.Daily, FirstRunAt, null);

        // Act
        var result = scheduledOrder.Cancel();

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheduledOrder.CancelledAt.Should().NotBeNull();
        scheduledOrder.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ReturnsFailure()
    {
        // Arrange
        var scheduledOrder = new ScheduledOrder(RestaurantId, RecurrenceType.Daily, FirstRunAt, null);
        scheduledOrder.Cancel();

        // Act
        var result = scheduledOrder.Cancel();

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
