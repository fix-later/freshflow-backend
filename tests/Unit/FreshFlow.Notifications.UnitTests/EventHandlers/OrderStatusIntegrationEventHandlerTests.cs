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
public sealed class OrderStatusIntegrationEventHandlerTests
{
    private readonly INotificationRecipientResolver _recipients =
        Substitute.For<INotificationRecipientResolver>();

    private readonly INotificationWriter _writer = Substitute.For<INotificationWriter>();

    [Fact]
    public async Task OrderConfirmed_WithResolvedRecipient_PersistsOrderStatusNotificationAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns(userId);
        SetupWriterReturn(userId);
        var sut = new OrderConfirmedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<OrderConfirmedIntegrationEventHandler>>());

        await sut.Handle(new OrderConfirmedIntegrationEvent(
            orderId,
            restaurantId,
            250_000m,
            DateTime.UtcNow), default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.order_status,
            "Đơn hàng đã được xác nhận",
            Arg.Is<string>(body => body.Contains(orderId.ToString(), StringComparison.Ordinal)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(payload =>
                payload["order_id"]!.Equals(orderId) &&
                payload["new_status"]!.Equals("confirmed") &&
                payload["total_amount"]!.Equals(250_000m)),
            default);
    }

    [Fact]
    public async Task OrderCancelled_WithResolvedRecipient_PersistsOrderStatusNotificationAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns(userId);
        SetupWriterReturn(userId);
        var sut = new OrderCancelledIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<OrderCancelledIntegrationEventHandler>>());

        await sut.Handle(new OrderCancelledIntegrationEvent(
            orderId,
            restaurantId,
            "out of stock",
            DateTime.UtcNow), default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.order_status,
            "Đơn hàng đã bị hủy",
            Arg.Is<string>(body => body.Contains(orderId.ToString(), StringComparison.Ordinal)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(payload =>
                payload["order_id"]!.Equals(orderId) &&
                payload["new_status"]!.Equals("cancelled") &&
                payload["cancellation_reason"]!.Equals("out of stock")),
            default);
    }

    [Fact]
    public async Task OrderConfirmed_RecipientMissing_SkipsPersistAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns((Guid?)null);
        var sut = new OrderConfirmedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<OrderConfirmedIntegrationEventHandler>>());

        await sut.Handle(new OrderConfirmedIntegrationEvent(
            Guid.NewGuid(),
            restaurantId,
            250_000m,
            DateTime.UtcNow), default);

        await _writer.DidNotReceive().WriteAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderCancelled_RecipientMissing_SkipsPersistAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns((Guid?)null);
        var sut = new OrderCancelledIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<OrderCancelledIntegrationEventHandler>>());

        await sut.Handle(new OrderCancelledIntegrationEvent(
            Guid.NewGuid(),
            restaurantId,
            "out of stock",
            DateTime.UtcNow), default);

        await _writer.DidNotReceive().WriteAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderConfirmed_WriterFails_DoesNotThrowAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns(userId);
        _writer.WriteAsync(
                Arg.Any<Guid>(),
                Arg.Any<NotificationType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Notification>(new InvalidOperationException("db down")));
        var sut = new OrderConfirmedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<OrderConfirmedIntegrationEventHandler>>());

        Func<Task> act = () => sut.Handle(new OrderConfirmedIntegrationEvent(
            Guid.NewGuid(),
            restaurantId,
            250_000m,
            DateTime.UtcNow), default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderCancelled_WriterFails_DoesNotThrowAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns(userId);
        _writer.WriteAsync(
                Arg.Any<Guid>(),
                Arg.Any<NotificationType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Notification>(new InvalidOperationException("db down")));
        var sut = new OrderCancelledIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<OrderCancelledIntegrationEventHandler>>());

        Func<Task> act = () => sut.Handle(new OrderCancelledIntegrationEvent(
            Guid.NewGuid(),
            restaurantId,
            "out of stock",
            DateTime.UtcNow), default);

        await act.Should().NotThrowAsync();
    }

    private void SetupWriterReturn(Guid userId)
    {
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
    }
}
