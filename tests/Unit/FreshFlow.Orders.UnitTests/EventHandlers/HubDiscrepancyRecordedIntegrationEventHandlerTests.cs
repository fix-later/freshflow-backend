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
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    [Fact]
    public async Task Handle_LockedPrice_RefundsFullDesiredAmountAndPublishesEventAsync()
    {
        // AUDIT-2026-08-23 C3: no more clamping to the account's current balance — the full
        // desired refund (quantity * locked price) is always requested; RefundAsync's
        // per-order cap is the only ceiling now.
        var order = ConfirmedOrderWithItem(out var itemId);
        var discrepancyId = Guid.NewGuid();
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        _creditService.RefundAsync(
                RestaurantId,
                order.Id,
                60_000m,
                Arg.Any<string?>(),
                default)
            .Returns(Result<CreditRefundDto>.Success(new CreditRefundDto(
                Guid.NewGuid(),
                new RestaurantCreditDto(RestaurantId, 1_000_000m, 0m, 1_000_000m, DateTime.UtcNow))));
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
            60_000m,
            Arg.Is<string>(note => note.Contains(discrepancyId.ToString(), StringComparison.Ordinal)),
            default);
        await _publisher.Received(1).Publish(
            Arg.Is<RestaurantRefundIssuedIntegrationEvent>(evt =>
                evt.RestaurantId == RestaurantId &&
                evt.OrderId == order.Id &&
                evt.OrderItemName == "Cà chua" &&
                evt.AffectedQuantity == 3m &&
                evt.RefundAmount == 60_000m),
            default);
    }

    [Fact]
    public async Task Handle_LockedPriceNull_SkipsRefundAndPublishAsync()
    {
        var order = DraftOrderWithItem(out var itemId);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
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
    public async Task Handle_NoOutstandingBalance_StillRefundsInFullAsync()
    {
        // AUDIT-2026-08-23 C3: a zero (or already-settled) balance no longer suppresses the
        // refund — it drives OutstandingBalance negative (FreshFlow owes the restaurant).
        var order = ConfirmedOrderWithItem(out var itemId);
        _orders.FindByIdAsync(order.Id, default).Returns(order);
        _creditService.RefundAsync(
                RestaurantId,
                order.Id,
                20_000m,
                Arg.Any<string?>(),
                default)
            .Returns(Result<CreditRefundDto>.Success(new CreditRefundDto(
                Guid.NewGuid(),
                new RestaurantCreditDto(RestaurantId, 1_000_000m, -20_000m, 1_020_000m, DateTime.UtcNow))));
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

        await _creditService.Received(1).RefundAsync(
            RestaurantId,
            order.Id,
            20_000m,
            Arg.Any<string?>(),
            default);
        await _publisher.Received(1).Publish(
            Arg.Is<RestaurantRefundIssuedIntegrationEvent>(evt => evt.RefundAmount == 20_000m),
            default);
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
