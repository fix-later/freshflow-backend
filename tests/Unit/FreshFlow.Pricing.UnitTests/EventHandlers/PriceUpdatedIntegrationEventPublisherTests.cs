using FreshFlow.Contracts;
using FreshFlow.Pricing.Application.EventHandlers;
using FreshFlow.Pricing.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class PriceUpdatedIntegrationEventPublisherTests
{
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly PriceUpdatedIntegrationEventPublisher _sut;

    private static readonly Guid MarketProductId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid UpdatedBy = Guid.NewGuid();
    private static readonly DateTime OccurredAt = new(2026, 6, 18, 7, 30, 0, DateTimeKind.Utc);

    public PriceUpdatedIntegrationEventPublisherTests() =>
        _sut = new PriceUpdatedIntegrationEventPublisher(_publisher);

    [Fact]
    public async Task Handle_PublishesIntegrationEventWithSameFieldsAsync()
    {
        var notification = new PriceUpdatedDomainEvent(
            MarketProductId, MarketId, ProductId, 10_000m, 12_000m, 50, UpdatedBy, OccurredAt);

        await _sut.Handle(notification, default);

        await _publisher.Received(1).Publish(
            Arg.Is<PriceUpdatedIntegrationEvent>(evt =>
                evt.MarketProductId == MarketProductId
                && evt.MarketId == MarketId
                && evt.ProductId == ProductId
                && evt.OldPrice == 10_000m
                && evt.NewPrice == 12_000m
                && evt.CurrentQuantity == 50
                && evt.UpdatedBy == UpdatedBy
                && evt.OccurredAt == OccurredAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenAsync()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var notification = new PriceUpdatedDomainEvent(
            MarketProductId, MarketId, ProductId, 10_000m, 12_000m, 50, UpdatedBy, OccurredAt);

        await _sut.Handle(notification, ct);

        await _publisher.Received(1).Publish(Arg.Any<PriceUpdatedIntegrationEvent>(), ct);
    }
}
