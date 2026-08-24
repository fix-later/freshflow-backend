using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Events;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class OrderProcurementShortfallDomainEventHandlerTests
{
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly OrderProcurementShortfallDomainEventHandler _sut;

    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OrderItemId = Guid.NewGuid();

    public OrderProcurementShortfallDomainEventHandlerTests()
    {
        _sut = new OrderProcurementShortfallDomainEventHandler(
            _creditService, _publisher, Substitute.For<ILogger<OrderProcurementShortfallDomainEventHandler>>());
    }

    private static OrderProcurementShortfallDomainEvent MakeEvent() => new(
        OrderId, RestaurantId, OrderItemId, "Cà chua", ShortfallQuantity: 4m, RefundAmount: 88_000m,
        OccurredAt: DateTime.UtcNow);

    [Fact]
    public async Task Handle_SuccessfulRefund_RefundsOnceAndPublishesRefundIssuedAsync()
    {
        // Arrange
        _creditService.RefundAsync(RestaurantId, OrderId, 88_000m, Arg.Any<string?>(), default)
            .Returns(Result<CreditRefundDto>.Success(new CreditRefundDto(
                Guid.NewGuid(),
                new RestaurantCreditDto(RestaurantId, 1_000_000m, 0m, 1_000_000m, DateTime.UtcNow))));

        // Act
        await _sut.Handle(MakeEvent(), default);

        // Assert
        await _creditService.Received(1).RefundAsync(
            RestaurantId, OrderId, 88_000m, Arg.Is<string>(note => note.Contains("Cà chua", StringComparison.Ordinal)),
            default);
        await _publisher.Received(1).Publish(
            Arg.Is<RestaurantRefundIssuedIntegrationEvent>(evt =>
                evt.RestaurantId == RestaurantId &&
                evt.OrderId == OrderId &&
                evt.OrderItemName == "Cà chua" &&
                evt.AffectedQuantity == 4m &&
                evt.RefundAmount == 88_000m),
            default);
    }

    [Fact]
    public async Task Handle_FailedRefund_LogsAndDoesNotThrowOrPublishAsync()
    {
        // Arrange — e.g. C3: refund capped at outstanding balance
        _creditService.RefundAsync(RestaurantId, OrderId, 88_000m, Arg.Any<string?>(), default)
            .Returns(Result<CreditRefundDto>.Failure(Error.Validation("INVALID_AMOUNT", "no balance")));

        // Act
        var act = () => _sut.Handle(MakeEvent(), default);

        // Assert
        await act.Should().NotThrowAsync();
        await _publisher.DidNotReceive().Publish(
            Arg.Any<RestaurantRefundIssuedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
