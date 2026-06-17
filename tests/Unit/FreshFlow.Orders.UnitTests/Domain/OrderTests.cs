using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrderTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsAllProperties()
    {
        // Act
        var order = new Order(RestaurantId, scheduledFor: null, notes: "Giao trước 6h sáng");

        // Assert
        order.RestaurantId.Should().Be(RestaurantId);
        order.Status.Should().Be(OrderStatus.Draft);
        order.PaymentStatus.Should().Be(OrderPaymentStatus.NotApplicable);
        order.TotalAmount.Should().Be(0);
        order.Notes.Should().Be("Giao trước 6h sáng");
        order.CancelledAt.Should().BeNull();
        order.CancellationReason.Should().BeNull();
        order.OrderGroupId.Should().BeNull();
        order.ScheduledOrderId.Should().BeNull();
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_RaisesOrderCreatedDomainEvent()
    {
        // Act
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Assert
        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Constructor_FromScheduledOrder_SetsScheduledOrderId()
    {
        // Arrange
        var scheduledOrderId = Guid.NewGuid();

        // Act
        var order = new Order(RestaurantId, scheduledFor: null, notes: null, scheduledOrderId: scheduledOrderId);

        // Assert
        order.ScheduledOrderId.Should().Be(scheduledOrderId);
    }

    // ── AddItem ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddItem_ValidArgs_AddsItemAndRecalculatesTotal()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Assert
        order.Items.Should().ContainSingle();
        order.TotalAmount.Should().Be(100_000m);
    }

    [Fact]
    public void AddItem_MultipleItems_TotalAmountIsSumOfSubtotals()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.AddItem(Guid.NewGuid(), "Hành lá", quantity: 2, unitPrice: 10_000m);

        // Assert
        order.TotalAmount.Should().Be(120_000m);
    }

    [Fact]
    public void AddItem_WhenOrderNotDraft_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.Confirm();

        // Act
        var result = order.AddItem(Guid.NewGuid(), "Hành lá", quantity: 2, unitPrice: 10_000m);

        // Assert
        result.IsFailure.Should().BeTrue();
        order.Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_ZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException(int quantity)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        var act = () => order.AddItem(MarketProductId, "Cà chua", quantity, unitPrice: 20_000m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    [Fact]
    public void AddItem_NegativeUnitPrice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        var act = () => order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: -1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unitPrice");
    }

    // ── UpdateItem ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateItem_ValidQuantity_UpdatesQuantityAndRecalculatesTotal()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        var itemId = order.Items.Single().Id;

        // Act
        var result = order.UpdateItem(itemId, quantity: 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Items.Single().Quantity.Should().Be(10);
        order.TotalAmount.Should().Be(200_000m);
    }

    [Fact]
    public void UpdateItem_WhenOrderNotDraft_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        var itemId = order.Items.Single().Id;
        order.Confirm();

        // Act
        var result = order.UpdateItem(itemId, quantity: 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
        order.Items.Single().Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateItem_ItemNotFound_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var result = order.UpdateItem(Guid.NewGuid(), quantity: 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateItem_ZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException(int quantity)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        var itemId = order.Items.Single().Id;

        // Act
        var act = () => order.UpdateItem(itemId, quantity);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    // ── RemoveItem ───────────────────────────────────────────────────────────

    [Fact]
    public void RemoveItem_ExistingItem_RemovesAndRecalculatesTotal()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.AddItem(Guid.NewGuid(), "Hành lá", quantity: 2, unitPrice: 10_000m);
        var itemId = order.Items.First().Id;

        // Act
        var result = order.RemoveItem(itemId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Items.Should().ContainSingle();
        order.TotalAmount.Should().Be(20_000m);
    }

    [Fact]
    public void RemoveItem_WhenOrderNotDraft_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        var itemId = order.Items.Single().Id;
        order.Confirm();

        // Act
        var result = order.RemoveItem(itemId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DRAFT");
        order.Items.Should().ContainSingle();
    }

    [Fact]
    public void RemoveItem_ItemNotFound_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var result = order.RemoveItem(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    // ── Confirm ──────────────────────────────────────────────────────────────

    [Fact]
    public void Confirm_FromDraftWithItems_TransitionsToConfirmed()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var result = order.Confirm();

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.PaymentStatus.Should().Be(OrderPaymentStatus.Outstanding);
    }

    [Fact]
    public void Confirm_WithoutItems_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        var result = order.Confirm();

        // Assert
        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        order.Confirm();

        // Act
        var result = order.Confirm();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Confirm_RaisesOrderConfirmedAndOrderStatusChangedDomainEvents()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        order.ClearDomainEvents();

        // Act
        order.Confirm();

        // Assert
        order.DomainEvents.Should().HaveCount(2);
        order.DomainEvents.Should().ContainItemsAssignableTo<FreshFlow.SharedKernel.Domain.IDomainEvent>();
        order.DomainEvents.OfType<OrderConfirmedDomainEvent>().Should().ContainSingle();
        order.DomainEvents.OfType<OrderStatusChangedDomainEvent>().Should().ContainSingle()
            .Which.NewStatus.Should().Be(OrderStatus.Confirmed);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromDraft_TransitionsToCancelled()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        var result = order.Cancel("Khách hàng đổi ý");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Khách hàng đổi ý");
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_FromConfirmed_TransitionsToCancelledAndWaivesPayment()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        order.Confirm();

        // Act
        var result = order.Cancel("Hết hàng");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.PaymentStatus.Should().Be(OrderPaymentStatus.Waived);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_FromTerminalState_ReturnsFailure(OrderStatus terminalStatus)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, terminalStatus);

        // Act
        var result = order.Cancel("test");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancel_RaisesOrderCancelledAndOrderStatusChangedDomainEvents()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.ClearDomainEvents();

        // Act
        order.Cancel("Khách hàng đổi ý");

        // Assert
        order.DomainEvents.OfType<OrderCancelledDomainEvent>().Should().ContainSingle();
        order.DomainEvents.OfType<OrderStatusChangedDomainEvent>().Should().ContainSingle()
            .Which.NewStatus.Should().Be(OrderStatus.Cancelled);
    }

    // ── AdvanceStatus (logistics state machine) ─────────────────────────────

    [Theory]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Batched)]
    [InlineData(OrderStatus.Batched, OrderStatus.PickedUp)]
    [InlineData(OrderStatus.PickedUp, OrderStatus.AtHub)]
    [InlineData(OrderStatus.AtHub, OrderStatus.Delivering)]
    [InlineData(OrderStatus.Delivering, OrderStatus.Delivered)]
    public void AdvanceStatus_ValidTransition_Succeeds(OrderStatus from, OrderStatus to)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, from);

        // Act
        var result = order.AdvanceStatus(to);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Batched)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Delivering)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    public void AdvanceStatus_InvalidTransition_ReturnsFailure(OrderStatus from, OrderStatus to)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, from);

        // Act
        var result = order.AdvanceStatus(to);

        // Assert
        result.IsFailure.Should().BeTrue();
        order.Status.Should().Be(from);
    }

    [Fact]
    public void AdvanceStatus_ToDelivered_SetsPaymentStatusSettledIsNotAutomatic()
    {
        // Arrange — delivery does not auto-settle credit; settlement is a separate AR flow (Phase 2a)
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, OrderStatus.Delivering);

        // Act
        order.AdvanceStatus(OrderStatus.Delivered);

        // Assert
        order.PaymentStatus.Should().Be(OrderPaymentStatus.Outstanding);
    }

    // ── Test helper — drives the aggregate through valid transitions to reach
    //    a target status without re-implementing the state machine in the test. ──
    private static void SetStatusForTest(Order order, OrderStatus target)
    {
        if (target == OrderStatus.Draft)
            return;

        if (order.Status == OrderStatus.Draft)
            order.Confirm();

        if (target == OrderStatus.Cancelled)
        {
            order.Cancel("test setup");
            return;
        }

        var pipeline = new[]
        {
            OrderStatus.Confirmed,
            OrderStatus.Batched,
            OrderStatus.PickedUp,
            OrderStatus.AtHub,
            OrderStatus.Delivering,
            OrderStatus.Delivered
        };

        var targetIndex = Array.IndexOf(pipeline, target);
        for (var i = 1; i <= targetIndex; i++)
            order.AdvanceStatus(pipeline[i]);
    }

    // ── DomainEvents ─────────────────────────────────────────────────────────

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        order.ClearDomainEvents();

        // Assert
        order.DomainEvents.Should().BeEmpty();
    }
}
