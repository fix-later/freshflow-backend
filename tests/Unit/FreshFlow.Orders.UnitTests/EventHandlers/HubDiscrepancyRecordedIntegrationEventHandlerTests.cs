using System.Reflection;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyRecordedIntegrationEventHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ICreditRepository _credits = Substitute.For<ICreditRepository>();
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    [Fact]
    public async Task Handle_LockedPriceAndOutstandingBalance_RefundsClampedAmountAndPublishesEventAsync()
    {
        var order = ConfirmedOrderWithItem(out var itemId);
        var account = new RestaurantCredit(RestaurantId, creditLimit: 1_000_000m);
        account.Charge(50_000m);
        var discrepancyId = Guid.NewGuid();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        _credits.FindAccountAsync(RestaurantId, default).Returns(account);
        _creditService.RefundAsync(
                RestaurantId,
                order.Id,
                50_000m,
                Arg.Any<string?>(),
                default)
            .Returns(Result<RestaurantCreditDto>.Success(
                new RestaurantCreditDto(RestaurantId, 1_000_000m, 0m, 1_000_000m, DateTime.UtcNow)));
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            discrepancyId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            itemId,
            3m,
            "MISSING",
            DateTime.UtcNow), default);

        await _creditService.Received(1).RefundAsync(
            RestaurantId,
            order.Id,
            50_000m,
            Arg.Is<string>(note => note.Contains(discrepancyId.ToString(), StringComparison.Ordinal)),
            default);
        await _publisher.Received(1).Publish(
            Arg.Is<RestaurantRefundIssuedIntegrationEvent>(evt =>
                evt.RestaurantId == RestaurantId &&
                evt.OrderId == order.Id &&
                evt.OrderItemName == "Cà chua" &&
                evt.AffectedQuantity == 3m &&
                evt.RefundAmount == 50_000m),
            default);
    }

    [Fact]
    public async Task Handle_LockedPriceNull_SkipsRefundAndPublishAsync()
    {
        var order = DraftOrderWithItem(out var itemId);
        var account = new RestaurantCredit(RestaurantId, creditLimit: 1_000_000m);
        account.Charge(50_000m);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        _credits.FindAccountAsync(RestaurantId, default).Returns(account);
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            itemId,
            1m,
            "DAMAGED",
            DateTime.UtcNow), default);

        await _creditService.DidNotReceive().RefundAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<decimal>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().Publish(
            Arg.Any<RestaurantRefundIssuedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoOutstandingBalance_SkipsRefundAndPublishAsync()
    {
        var order = ConfirmedOrderWithItem(out var itemId);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        _credits.FindAccountAsync(RestaurantId, default)
            .Returns(new RestaurantCredit(RestaurantId, creditLimit: 1_000_000m));
        var sut = CreateSut();

        await sut.Handle(new HubDiscrepancyRecordedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            order.Id,
            itemId,
            1m,
            "PARTIAL",
            DateTime.UtcNow), default);

        await _creditService.DidNotReceive().RefundAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<decimal>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().Publish(
            Arg.Any<RestaurantRefundIssuedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_DoesNotDependOnPaymentGateway()
    {
        var parameterNames = typeof(HubDiscrepancyRecordedIntegrationEventHandler)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name);

        parameterNames.Should().NotContain(name =>
            name.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gateway", StringComparison.OrdinalIgnoreCase));
    }

    private HubDiscrepancyRecordedIntegrationEventHandler CreateSut() =>
        new(
            _orders,
            _credits,
            _creditService,
            _publisher,
            Substitute.For<ILogger<HubDiscrepancyRecordedIntegrationEventHandler>>());

    private static Order ConfirmedOrderWithItem(out Guid itemId)
    {
        var order = DraftOrderWithItem(out itemId);
        order.Confirm();
        return order;
    }

    private static Order DraftOrderWithItem(out Guid itemId)
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        itemId = order.Items.Single().Id;
        return order;
    }
}
