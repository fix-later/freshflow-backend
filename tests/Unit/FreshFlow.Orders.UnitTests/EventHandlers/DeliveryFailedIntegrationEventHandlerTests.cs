using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class DeliveryFailedIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly DeliveryFailedIntegrationEventHandler _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    public DeliveryFailedIntegrationEventHandlerTests()
    {
        _orders.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _creditService.RefundAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditRefundDto>.Success(new CreditRefundDto(
                Guid.NewGuid(),
                new RestaurantCreditDto(RestaurantId, 1_000_000m, 0m, 1_000_000m, DateTime.UtcNow))));

        _sut = new DeliveryFailedIntegrationEventHandler(
            _orders, _creditService, Substitute.For<ILogger<DeliveryFailedIntegrationEventHandler>>());
    }

    private static Order MakeDeliveringOrder()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        order.AdvanceStatus(OrderStatus.PickedUp);
        order.AdvanceStatus(OrderStatus.AtHub);
        order.AdvanceStatus(OrderStatus.Delivering);
        return order;
    }

    [Fact]
    public async Task Handle_DeliveringOrder_CancelsAndRefundsTotalAmountOnceAsync()
    {
        // Arrange
        var order = MakeDeliveringOrder();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var notification = new DeliveryFailedIntegrationEvent(
            order.Id, Guid.NewGuid(), Guid.NewGuid(), "Không liên lạc được nhà hàng", DateTime.UtcNow);

        // Act
        await _sut.Handle(notification, default);

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        await _creditService.Received(1).RefundAsync(
            RestaurantId, order.Id, 100_000m, "Delivery failed", default);
    }

    [Fact]
    public async Task Handle_RepeatedEvent_IsIdempotentAndDoesNotRefundTwiceAsync()
    {
        // Arrange — the event can be redelivered; an already-Cancelled order is a no-op
        var order = MakeDeliveringOrder();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var notification = new DeliveryFailedIntegrationEvent(
            order.Id, Guid.NewGuid(), Guid.NewGuid(), "reason", DateTime.UtcNow);

        // Act
        await _sut.Handle(notification, default);
        await _sut.Handle(notification, default);

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        await _creditService.Received(1).RefundAsync(
            RestaurantId, order.Id, 100_000m, "Delivery failed", default);
    }

    [Fact]
    public async Task Handle_NeverReleasesOrConsumesStockAsync()
    {
        // Arrange — reservation was already settled at procurement handover; nothing to release
        var order = MakeDeliveringOrder();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        var notification = new DeliveryFailedIntegrationEvent(
            order.Id, Guid.NewGuid(), Guid.NewGuid(), "reason", DateTime.UtcNow);

        // Act
        await _sut.Handle(notification, default);

        // Assert
        await _orders.DidNotReceiveWithAnyArgs().ReleaseStockAsync(default!, default);
        await _orders.DidNotReceiveWithAnyArgs().ConsumeStockAsync(default!, default);
        await _orders.DidNotReceiveWithAnyArgs().TryReserveStockAsync(default!, default);
    }
}
