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
public sealed class CreditLimitThresholdReachedIntegrationEventHandlerTests
{
    private readonly INotificationRecipientResolver _recipients =
        Substitute.For<INotificationRecipientResolver>();

    private readonly INotificationWriter _writer = Substitute.For<INotificationWriter>();

    private readonly CreditLimitThresholdReachedIntegrationEventHandler _sut;

    public CreditLimitThresholdReachedIntegrationEventHandlerTests()
    {
        var logger = Substitute.For<ILogger<CreditLimitThresholdReachedIntegrationEventHandler>>();
        _sut = new CreditLimitThresholdReachedIntegrationEventHandler(_recipients, _writer, logger);
    }

    [Fact]
    public async Task Handle_WithResolvedRecipient_PersistsCreditAlertNotificationAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var notification = new CreditLimitThresholdReachedIntegrationEvent(
            restaurantId,
            "warning",
            0.85m,
            85m,
            100m,
            DateTime.UtcNow);
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns(userId);
        _writer.WriteAsync(
                userId,
                NotificationType.credit_alert,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(),
                default)
            .Returns(call => new Notification(
                userId,
                (NotificationType)call[1]!,
                (string)call[2]!,
                (string)call[3]!,
                null));

        await _sut.Handle(notification, default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.credit_alert,
            "Cảnh báo hạn mức tín dụng",
            Arg.Is<string>(body => body.Contains("85", StringComparison.Ordinal)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(payload =>
                payload["level"]!.Equals("warning") &&
                payload["utilization"]!.Equals(0.85m) &&
                payload["outstanding"]!.Equals(85m) &&
                payload["limit"]!.Equals(100m)),
            default);
    }

    [Fact]
    public async Task Handle_RecipientMissing_SkipsPersistAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveUserIdByRestaurantIdAsync(restaurantId, default)
            .Returns((Guid?)null);
        var notification = new CreditLimitThresholdReachedIntegrationEvent(
            restaurantId,
            "warning",
            0.85m,
            85m,
            100m,
            DateTime.UtcNow);

        await _sut.Handle(notification, default);

        await _writer.DidNotReceive().WriteAsync(
            Arg.Any<Guid>(),
            Arg.Any<NotificationType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WriterFails_DoesNotThrowAsync()
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
        var notification = new CreditLimitThresholdReachedIntegrationEvent(
            restaurantId,
            "exceeded",
            1.1m,
            110m,
            100m,
            DateTime.UtcNow);

        Func<Task> act = () => _sut.Handle(notification, default);

        await act.Should().NotThrowAsync();
    }
}
