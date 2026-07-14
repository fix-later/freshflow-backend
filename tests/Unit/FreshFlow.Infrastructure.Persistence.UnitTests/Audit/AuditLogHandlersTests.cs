using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence.Audit;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Infrastructure.Persistence.UnitTests.Audit;

[Trait("Category", "Unit")]
public sealed class AuditLogHandlersTests
{
    private readonly IAuditLogWriter _writer = Substitute.For<IAuditLogWriter>();
    private static readonly DateTime OccurredAt = new(2026, 6, 18, 7, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OrderCancelledHandler_WritesOrderCancelledActionAsync()
    {
        var orderId = Guid.NewGuid();
        var sut = new OrderCancelledAuditLogHandler(_writer);

        await sut.Handle(
            new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "out of stock", OccurredAt), default);

        await _writer.Received(1).WriteAsync(
            null, "order_cancelled", "order", orderId, Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreditLimitThresholdReachedHandler_WritesCreditLimitActionAsync()
    {
        var restaurantId = Guid.NewGuid();
        var sut = new CreditLimitThresholdReachedAuditLogHandler(_writer);

        await sut.Handle(
            new CreditLimitThresholdReachedIntegrationEvent(restaurantId, "warning", 0.8m, 80m, 100m, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null, "credit_limit_threshold_reached", "restaurant", restaurantId,
            Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurantRefundIssuedHandler_WritesRefundActionAsync()
    {
        var restaurantId = Guid.NewGuid();
        var sut = new RestaurantRefundIssuedAuditLogHandler(_writer);

        await sut.Handle(
            new RestaurantRefundIssuedIntegrationEvent(
                restaurantId, Guid.NewGuid(), "Cà chua", 2m, 40_000m, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null, "restaurant_refund_issued", "restaurant", restaurantId,
            Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PriceUpdatedHandler_WritesPriceUpdatedActionWithActorAsync()
    {
        var marketProductId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();
        var sut = new PriceUpdatedAuditLogHandler(_writer);

        await sut.Handle(
            new PriceUpdatedIntegrationEvent(
                marketProductId, Guid.NewGuid(), Guid.NewGuid(), 10_000m, 12_000m, 50, updatedBy, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            updatedBy, "price_updated", "market_product", marketProductId,
            Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }
}
