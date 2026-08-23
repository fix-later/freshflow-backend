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
        order.ConfirmedReceiptAt.Should().BeNull();
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

    [Fact]
    public void CaptureDeliveryAddress_CalledTwice_KeepsFirstSnapshot()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        var firstAddressId = Guid.NewGuid();

        order.CaptureDeliveryAddress(
            firstAddressId, "First", "0901", "1 First Street", 10.1m, 106.1m);
        var second = order.CaptureDeliveryAddress(
            Guid.NewGuid(), "Second", "0902", "2 Second Street", 10.2m, 106.2m);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("DELIVERY_ADDRESS_ALREADY_CAPTURED");
        order.DeliveryAddressId.Should().Be(firstAddressId);
        order.DeliveryAddressLine.Should().Be("1 First Street");
    }

    [Fact]
    public void ApplyConfirmationPricing_PartialDeliverySnapshot_ReturnsFailure()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 1, 20_000m);

        var result = order.ApplyConfirmationPricing(
            new Dictionary<Guid, OrderItemTaxSnapshot>
            {
                [MarketProductId] = new("KCT", 0m)
            },
            0m,
            0m,
            routingProvider: "GOONG");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_DELIVERY_SNAPSHOT");
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
    public void CanConfirm_FromDraftWithItems_ReturnsSuccessWithoutMutatingOrder()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var result = order.CanConfirm();

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Draft);
        order.PaymentStatus.Should().Be(OrderPaymentStatus.NotApplicable);
        order.Items.Single().LockedUnitPrice.Should().BeNull();
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
    public void CanConfirm_WithoutItems_ReturnsOrderEmpty()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);

        // Act
        var result = order.CanConfirm();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_EMPTY");
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

    [Fact]
    public void RescheduleFor_DraftOrder_UpdatesScheduledFor()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        var newDate = new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var result = order.RescheduleFor(newDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.ScheduledFor.Should().Be(newDate);
    }

    [Fact]
    public void RescheduleFor_CancelledOrder_ReturnsFailure()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.Cancel("test");
        var newDate = new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var result = order.RescheduleFor(newDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_CANNOT_RESCHEDULE");
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
    [InlineData(OrderStatus.Batched)]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.AtHub)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_FromBatchedOrLaterOrTerminalState_ReturnsOrderNotCancellable(OrderStatus terminalStatus)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, terminalStatus);

        // Act
        var result = order.Cancel("test");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_CANCELLABLE");
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

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Batched)]
    public void CancelWithSession_ConfirmedOrBatchedOrder_CancelsAndWaivesDebt(OrderStatus status)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, status);

        // Act
        var result = order.CancelWithSession("Phiên chợ bị hủy");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Phiên chợ bị hủy");
        order.DomainEvents.OfType<OrderCancelledDomainEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(OrderStatus.PickedUp)]
    [InlineData(OrderStatus.AtHub)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void CancelWithSession_AfterPickup_ReturnsOrderNotCancellable(OrderStatus status)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, status);

        // Act
        var result = order.CancelWithSession("test");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_CANCELLABLE");
    }

    [Fact]
    public void CancelForFailedDelivery_FromDelivering_CancelsAndWaivesDebt()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, OrderStatus.Delivering);

        // Act
        var result = order.CancelForFailedDelivery("Giao hàng thất bại");

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAt.Should().NotBeNull();
        order.PaymentStatus.Should().Be(OrderPaymentStatus.Waived);
        order.DomainEvents.OfType<OrderCancelledDomainEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.AtHub)]
    [InlineData(OrderStatus.Delivered)]
    public void CancelForFailedDelivery_FromNonDelivering_ReturnsOrderNotCancellable(OrderStatus status)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, status);

        // Act
        var result = order.CancelForFailedDelivery("test");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_CANCELLABLE");
    }

    [Fact]
    public void AdvanceStatus_ToCancelled_ReturnsInvalidTransition()
    {
        // Arrange — cancelling must go through Cancel so the debt waiver is not skipped.
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        order.Confirm();

        // Act
        var result = order.AdvanceStatus(OrderStatus.Cancelled);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_INVALID_TRANSITION");
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    // ── RecordActualQuantity ────────────────────────────────────────────────

    [Fact]
    public void RecordActualQuantity_ConfirmedOrder_UpdatesItemActualQuantity()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.Confirm();
        var itemId = order.Items.Single().Id;

        // Act
        var result = order.RecordActualQuantity(itemId, 4.5m);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Items.Single().ActualQuantity.Should().Be(4.5m);
    }

    [Fact]
    public void RecordActualQuantity_DraftOrder_ReturnsOrderCannotAdjust()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        var itemId = order.Items.Single().Id;

        // Act
        var result = order.RecordActualQuantity(itemId, 4m);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_CANNOT_ADJUST");
    }

    [Fact]
    public void RecordActualQuantity_CancelledOrder_ReturnsOrderCannotAdjust()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.Cancel("test");
        var itemId = order.Items.Single().Id;

        // Act
        var result = order.RecordActualQuantity(itemId, 4m);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_CANNOT_ADJUST");
    }

    [Fact]
    public void RecordActualQuantity_ItemNotFound_ReturnsOrderItemNotFound()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.Confirm();

        // Act
        var result = order.RecordActualQuantity(Guid.NewGuid(), 4m);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void RecordActualQuantity_InvalidQuantity_ReturnsInvalidActualQuantity(decimal actualQuantity)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);
        order.Confirm();
        var itemId = order.Items.Single().Id;

        // Act
        var result = order.RecordActualQuantity(itemId, actualQuantity);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_ACTUAL_QUANTITY");
    }

    // ── ApplyProcurementActuals (C1 shortfall refund) ───────────────────────

    /// <summary>Confirms with a 10% VAT rate and advances to Batched, ready for actuals.</summary>
    private static (Order Order, Guid ItemId) MakeBatchedOrderWithVat(
        decimal quantity = 10m, decimal unitPrice = 20_000m, decimal vatPercent = 10m)
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", (int)quantity, unitPrice);
        order.ApplyConfirmationPricing(
            new Dictionary<Guid, OrderItemTaxSnapshot> { [MarketProductId] = new("10", vatPercent) },
            0m, 0m);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        return (order, order.Items.Single().Id);
    }

    [Fact]
    public void ApplyProcurementActuals_Shortfall_RaisesOneEventWithGoodsPlusVat()
    {
        // Arrange — ordered 10 @ 20,000 with 10% VAT; agent only bought 6
        var (order, itemId) = MakeBatchedOrderWithVat();
        order.ClearDomainEvents();

        // Act
        var result = order.ApplyProcurementActuals(new Dictionary<Guid, OrderItemProcurementActual>
        {
            [itemId] = new(6m, 20_000m)
        });

        // Assert — shortfall 4 * 20,000 = 80,000 goods + 10% VAT (8,000) = 88,000
        result.IsSuccess.Should().BeTrue();
        var evt = order.DomainEvents.OfType<OrderProcurementShortfallDomainEvent>().Should().ContainSingle().Which;
        evt.OrderId.Should().Be(order.Id);
        evt.RestaurantId.Should().Be(RestaurantId);
        evt.OrderItemId.Should().Be(itemId);
        evt.ShortfallQuantity.Should().Be(4m);
        evt.RefundAmount.Should().Be(88_000m);
    }

    [Fact]
    public void ApplyProcurementActuals_FullDelivery_RaisesNoShortfallEvent()
    {
        // Arrange
        var (order, itemId) = MakeBatchedOrderWithVat();
        order.ClearDomainEvents();

        // Act
        var result = order.ApplyProcurementActuals(new Dictionary<Guid, OrderItemProcurementActual>
        {
            [itemId] = new(10m, 20_000m)
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.DomainEvents.OfType<OrderProcurementShortfallDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void ApplyProcurementActuals_ZeroActualQuantity_RaisesEventForWholeLine()
    {
        // Arrange — agent bought nothing at all for this line
        var (order, itemId) = MakeBatchedOrderWithVat(quantity: 5m, unitPrice: 20_000m, vatPercent: 0m);
        order.ClearDomainEvents();

        // Act
        var result = order.ApplyProcurementActuals(new Dictionary<Guid, OrderItemProcurementActual>
        {
            [itemId] = new(0m, null)
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        var evt = order.DomainEvents.OfType<OrderProcurementShortfallDomainEvent>().Should().ContainSingle().Which;
        evt.ShortfallQuantity.Should().Be(5m);
        evt.RefundAmount.Should().Be(100_000m); // 5 * 20,000, 0% VAT
    }

    [Fact]
    public void ApplyProcurementActuals_Shortfall_DoesNotMutateOrderTotals()
    {
        // Arrange — TotalAmount/SubtotalAmount/VatAmount are the immutable confirmation snapshot
        var (order, itemId) = MakeBatchedOrderWithVat();
        var totalBefore = order.TotalAmount;
        var subtotalBefore = order.SubtotalAmount;
        var vatBefore = order.VatAmount;

        // Act
        order.ApplyProcurementActuals(new Dictionary<Guid, OrderItemProcurementActual>
        {
            [itemId] = new(6m, 20_000m)
        });

        // Assert
        order.TotalAmount.Should().Be(totalBefore);
        order.SubtotalAmount.Should().Be(subtotalBefore);
        order.VatAmount.Should().Be(vatBefore);
    }

    [Fact]
    public void ApplyProcurementActuals_ItemWithNoLockedUnitPrice_RaisesNoEvent()
    {
        // Arrange — LockedUnitPrice is unreachably null via the public API (Confirm() always
        // locks pricing); reflection simulates the defence-in-depth guard's edge case directly.
        var (order, itemId) = MakeBatchedOrderWithVat();
        var item = order.Items.Single();
        typeof(OrderItem).GetProperty(nameof(OrderItem.LockedUnitPrice))!.SetValue(item, null);
        order.ClearDomainEvents();

        // Act
        var result = order.ApplyProcurementActuals(new Dictionary<Guid, OrderItemProcurementActual>
        {
            [itemId] = new(6m, 20_000m)
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.DomainEvents.OfType<OrderProcurementShortfallDomainEvent>().Should().BeEmpty();
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

    // ── ConfirmReceipt ──────────────────────────────────────────────────────

    [Fact]
    public void ConfirmReceipt_DeliveredOrder_SetsConfirmedReceiptAt()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, OrderStatus.Delivered);
        var confirmedAt = new DateTime(2026, 6, 18, 7, 0, 0, DateTimeKind.Utc);

        // Act
        var result = order.ConfirmReceipt(confirmedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
        order.ConfirmedReceiptAt.Should().Be(confirmedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Delivering)]
    [InlineData(OrderStatus.Cancelled)]
    public void ConfirmReceipt_WhenOrderNotDelivered_ReturnsOrderNotDelivered(OrderStatus status)
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, status);

        // Act
        var result = order.ConfirmReceipt();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_DELIVERED");
        order.ConfirmedReceiptAt.Should().BeNull();
    }

    [Fact]
    public void ConfirmReceipt_WhenAlreadyConfirmed_ReturnsReceiptAlreadyConfirmed()
    {
        // Arrange
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: 20_000m);
        SetStatusForTest(order, OrderStatus.Delivered);
        order.ConfirmReceipt();

        // Act
        var result = order.ConfirmReceipt();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_RECEIPT_ALREADY_CONFIRMED");
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
