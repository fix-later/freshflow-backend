using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.EventHandlers;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class RestaurantRefundIssuedIntegrationEventHandlerTests
{
    private readonly INotificationRecipientResolver _recipients =
        Substitute.For<INotificationRecipientResolver>();

    private readonly INotificationWriter _writer = Substitute.For<INotificationWriter>();

    [Fact]
    public async Task Handle_WithResolvedRecipient_PersistsRefundNotificationPayloadAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns(userId);
        _writer.WriteAsync(
                Arg.Any<Guid>(),
                Arg.Any<NotificationType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new Notification(
                userId,
                (NotificationType)call[1]!,
                (string)call[2]!,
                (string)call[3]!,
                null));
        var sut = new RestaurantRefundIssuedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<RestaurantRefundIssuedIntegrationEventHandler>>());
        var occurredAt = DateTime.UtcNow;

        await sut.Handle(new RestaurantRefundIssuedIntegrationEvent(
            restaurantId,
            orderId,
            "Cà chua",
            2m,
            40_000m,
            occurredAt), default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.credit_alert,
            "Đã hoàn tiền do sai lệch tại hub",
            Arg.Is<string>(body => body.Contains(orderId.ToString(), StringComparison.Ordinal)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(payload =>
                payload["order_id"]!.Equals(orderId) &&
                payload["item_name"]!.Equals("Cà chua") &&
                payload["affected_quantity"]!.Equals(2m) &&
                payload["refund_amount"]!.Equals(40_000m) &&
                payload["occurred_at"]!.Equals(occurredAt)),
            default);
    }

    [Fact]
    public async Task Handle_RecipientMissing_SkipsPersistAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns((Guid?)null);
        var sut = new RestaurantRefundIssuedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<RestaurantRefundIssuedIntegrationEventHandler>>());

        await sut.Handle(new RestaurantRefundIssuedIntegrationEvent(
            restaurantId,
            Guid.NewGuid(),
            "Cà chua",
            1m,
            20_000m,
            DateTime.UtcNow), default);

        await _writer.DidNotReceive().WriteAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<CancellationToken>());
    }
}
