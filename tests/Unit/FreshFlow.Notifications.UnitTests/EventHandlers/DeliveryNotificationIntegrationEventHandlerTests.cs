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
public sealed class DeliveryNotificationIntegrationEventHandlerTests
{
    private readonly INotificationRecipientResolver _recipients =
        Substitute.For<INotificationRecipientResolver>();

    private readonly INotificationWriter _writer = Substitute.For<INotificationWriter>();

    [Fact]
    public async Task DeliveryStarted_WithResolvedRecipient_PersistsDeliveryUpdateNotificationAsync()
    {
        var userId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        _recipients.ResolveUserIdByOrderIdAsync(orderId, default).Returns(userId);
        SetupWriterReturn(userId);
        var sut = new DeliveryStartedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryStartedIntegrationEventHandler>>());

        await sut.Handle(new DeliveryStartedIntegrationEvent(routeId, [orderId], occurredAt), default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.delivery_update,
            "Đơn hàng đang được giao",
            Arg.Is<string>(body => body.Contains(orderId.ToString(), StringComparison.Ordinal)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(payload =>
                payload["order_id"]!.Equals(orderId) &&
                payload["route_id"]!.Equals(routeId) &&
                payload["status"]!.Equals("started") &&
                payload["occurred_at"]!.Equals(occurredAt)),
            default);
    }

    [Fact]
    public async Task DeliveryStarted_RecipientMissing_SkipsPersistAsync()
    {
        var orderId = Guid.NewGuid();
        _recipients.ResolveUserIdByOrderIdAsync(orderId, default).Returns((Guid?)null);
        var sut = new DeliveryStartedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryStartedIntegrationEventHandler>>());

        await sut.Handle(new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [orderId], DateTime.UtcNow), default);

        await _writer.DidNotReceive().WriteAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryStarted_WriterFails_DoesNotThrowAsync()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _recipients.ResolveUserIdByOrderIdAsync(orderId, default).Returns(userId);
        _writer.WriteAsync(
                Arg.Any<Guid>(),
                Arg.Any<NotificationType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Notification>(new InvalidOperationException("db down")));
        var sut = new DeliveryStartedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryStartedIntegrationEventHandler>>());

        var act = async () => await sut.Handle(
            new DeliveryStartedIntegrationEvent(Guid.NewGuid(), [orderId], DateTime.UtcNow),
            default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeliveryStarted_OneOrderWriterFails_StillPersistsRemainingOrdersAsync()
    {
        var routeId = Guid.NewGuid();
        var failingOrderId = Guid.NewGuid();
        var failingUserId = Guid.NewGuid();
        var okOrderId = Guid.NewGuid();
        var okUserId = Guid.NewGuid();
        _recipients.ResolveUserIdByOrderIdAsync(failingOrderId, default).Returns(failingUserId);
        _recipients.ResolveUserIdByOrderIdAsync(okOrderId, default).Returns(okUserId);
        _writer.WriteAsync(
                failingUserId,
                Arg.Any<NotificationType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Notification>(new InvalidOperationException("db down")));
        SetupWriterReturn(okUserId);
        var sut = new DeliveryStartedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryStartedIntegrationEventHandler>>());

        await sut.Handle(
            new DeliveryStartedIntegrationEvent(routeId, [failingOrderId, okOrderId], DateTime.UtcNow),
            default);

        await _writer.Received(1).WriteAsync(
            okUserId,
            NotificationType.delivery_update,
            "Đơn hàng đang được giao",
            Arg.Is<string>(body => body.Contains(okOrderId.ToString(), StringComparison.Ordinal)),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            default);
    }

    [Fact]
    public async Task DeliveryCompleted_WithResolvedRecipient_PersistsDeliveryUpdateNotificationAsync()
    {
        var userId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var actualArrival = DateTime.UtcNow.AddMinutes(-1);
        var occurredAt = DateTime.UtcNow;
        _recipients.ResolveUserIdByOrderIdAsync(orderId, default).Returns(userId);
        SetupWriterReturn(userId);
        var sut = new DeliveryCompletedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

        await sut.Handle(new DeliveryCompletedIntegrationEvent(
            orderId,
            routeId,
            actualArrival,
            occurredAt), default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.delivery_update,
            "Đơn hàng đã được giao",
            Arg.Is<string>(body => body.Contains(orderId.ToString(), StringComparison.Ordinal)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(payload =>
                payload["order_id"]!.Equals(orderId) &&
                payload["route_id"]!.Equals(routeId) &&
                payload["status"]!.Equals("delivered") &&
                payload["actual_arrival_at"]!.Equals(actualArrival) &&
                payload["occurred_at"]!.Equals(occurredAt)),
            default);
    }

    [Fact]
    public async Task DeliveryCompleted_RecipientMissing_SkipsPersistAsync()
    {
        var orderId = Guid.NewGuid();
        _recipients.ResolveUserIdByOrderIdAsync(orderId, default).Returns((Guid?)null);
        var sut = new DeliveryCompletedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

        await sut.Handle(new DeliveryCompletedIntegrationEvent(
            orderId,
            Guid.NewGuid(),
            DateTime.UtcNow,
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
    public async Task DeliveryCompleted_WriterFails_DoesNotThrowAsync()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _recipients.ResolveUserIdByOrderIdAsync(orderId, default).Returns(userId);
        _writer.WriteAsync(
                Arg.Any<Guid>(),
                Arg.Any<NotificationType>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Notification>(new InvalidOperationException("db down")));
        var sut = new DeliveryCompletedIntegrationEventHandler(
            _recipients,
            _writer,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

        var act = async () => await sut.Handle(new DeliveryCompletedIntegrationEvent(
            orderId,
            Guid.NewGuid(),
            DateTime.UtcNow,
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
