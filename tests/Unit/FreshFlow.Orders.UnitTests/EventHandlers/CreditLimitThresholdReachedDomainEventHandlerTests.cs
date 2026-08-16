using FreshFlow.Contracts;
using FreshFlow.Orders.Application.EventHandlers;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class CreditLimitThresholdReachedDomainEventHandlerTests
{
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly CreditLimitThresholdReachedDomainEventHandler _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly DateTime OccurredAt = new(2026, 6, 18, 7, 30, 0, DateTimeKind.Utc);

    public CreditLimitThresholdReachedDomainEventHandlerTests()
    {
        _sut = new CreditLimitThresholdReachedDomainEventHandler(_publisher);
    }

    [Fact]
    public async Task Handle_WarningLevel_PublishesIntegrationEventWithSnakeCaseLevelAsync()
    {
        var notification = new CreditLimitThresholdReachedDomainEvent(
            RestaurantId, CreditAlertLevel.Warning, 0.8m, 80m, 100m, OccurredAt);

        await _sut.Handle(notification, default);

        await _publisher.Received(1).Publish(
            Arg.Is<CreditLimitThresholdReachedIntegrationEvent>(evt =>
                evt.RestaurantId == RestaurantId
                && evt.Level == "warning"
                && evt.Utilization == 0.8m
                && evt.OutstandingBalance == 80m
                && evt.CreditLimit == 100m
                && evt.OccurredAt == OccurredAt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExceededLevel_PublishesIntegrationEventWithSnakeCaseLevelAsync()
    {
        var notification = new CreditLimitThresholdReachedDomainEvent(
            RestaurantId, CreditAlertLevel.Exceeded, 1.0m, 100m, 100m, OccurredAt);

        await _sut.Handle(notification, default);

        await _publisher.Received(1).Publish(
            Arg.Is<CreditLimitThresholdReachedIntegrationEvent>(evt => evt.Level == "exceeded"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenAsync()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var notification = new CreditLimitThresholdReachedDomainEvent(
            RestaurantId, CreditAlertLevel.Warning, 0.8m, 80m, 100m, OccurredAt);

        await _sut.Handle(notification, ct);

        await _publisher.Received(1).Publish(Arg.Any<CreditLimitThresholdReachedIntegrationEvent>(), ct);
    }
}
