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
        scheduledOrder.DeliveryAddressId.Should().BeNull();
        scheduledOrder.Items.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithDeliveryAddressId_SetsIt()
    {
        var deliveryAddressId = Guid.NewGuid();

        var scheduledOrder = new ScheduledOrder(
            RestaurantId, RecurrenceType.Daily, FirstRunAt, notes: null, deliveryAddressId);

        scheduledOrder.DeliveryAddressId.Should().Be(deliveryAddressId);
    }

    [Fact]
    public void AddItem_ValidArgs_AppendsToItems()
    {
        var scheduledOrder = new ScheduledOrder(RestaurantId, RecurrenceType.Daily, FirstRunAt, null);
        var marketProductId = Guid.NewGuid();

        scheduledOrder.AddItem(marketProductId, 3);

        scheduledOrder.Items.Should().ContainSingle();
        scheduledOrder.Items.Single().MarketProductId.Should().Be(marketProductId);
        scheduledOrder.Items.Single().Quantity.Should().Be(3);
    }

    [Fact]
    public void ReplaceItems_ClearsAndAddsNewSet()
    {
        var scheduledOrder = new ScheduledOrder(RestaurantId, RecurrenceType.Daily, FirstRunAt, null);
        scheduledOrder.AddItem(Guid.NewGuid(), 1);
        var replacementProductId = Guid.NewGuid();

        scheduledOrder.ReplaceItems([(replacementProductId, 5)]);

        scheduledOrder.Items.Should().ContainSingle();
        scheduledOrder.Items.Single().MarketProductId.Should().Be(replacementProductId);
        scheduledOrder.Items.Single().Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateSchedule_DeliveryAddressIdOmitted_KeepsExisting()
    {
        var originalAddressId = Guid.NewGuid();
        var scheduledOrder = new ScheduledOrder(
            RestaurantId, RecurrenceType.Daily, FirstRunAt, null, originalAddressId);

        var result = scheduledOrder.UpdateSchedule(RecurrenceType.Weekly, FirstRunAt.AddDays(1), "note");

        result.IsSuccess.Should().BeTrue();
        scheduledOrder.DeliveryAddressId.Should().Be(originalAddressId);
    }

    [Fact]
    public void UpdateSchedule_DeliveryAddressIdProvided_ReplacesExisting()
    {
        var scheduledOrder = new ScheduledOrder(
            RestaurantId, RecurrenceType.Daily, FirstRunAt, null, Guid.NewGuid());
        var newAddressId = Guid.NewGuid();

        var result = scheduledOrder.UpdateSchedule(
            RecurrenceType.Weekly, FirstRunAt.AddDays(1), "note", newAddressId);

        result.IsSuccess.Should().BeTrue();
        scheduledOrder.DeliveryAddressId.Should().Be(newAddressId);
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
