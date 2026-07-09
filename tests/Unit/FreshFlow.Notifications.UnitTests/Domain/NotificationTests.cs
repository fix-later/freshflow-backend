using FluentAssertions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class NotificationTests
{
    [Fact]
    public void Constructor_EmptyUserId_ThrowsArgumentException()
    {
        var act = () => new Notification(
            Guid.Empty,
            NotificationType.system,
            "Title",
            "Body",
            null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Constructor_WhitespaceTitle_ThrowsArgumentException()
    {
        var act = () => new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            " ",
            "Body",
            null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("title");
    }

    [Fact]
    public void Constructor_WhitespaceBody_ThrowsArgumentException()
    {
        var act = () => new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            " ",
            null);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("body");
    }

    [Fact]
    public void Constructor_SetsPendingSendMetadata()
    {
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            "Body",
            null);

        notification.SendStatus.Should().Be(NotificationSendStatus.pending);
        notification.AttemptCount.Should().Be(0);
        notification.LastAttemptAt.Should().BeNull();
        notification.FailedReason.Should().BeNull();
    }

    [Fact]
    public void MarkSent_SetsSentMetadataAndClearsFailureReason()
    {
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            "Body",
            null);
        notification.MarkFailed("push failed");
        var failedAttemptAt = notification.LastAttemptAt;

        notification.MarkSent();

        notification.SendStatus.Should().Be(NotificationSendStatus.sent);
        notification.AttemptCount.Should().Be(2);
        notification.LastAttemptAt.Should().NotBeNull();
        notification.LastAttemptAt.Should().BeOnOrAfter(failedAttemptAt!.Value);
        notification.FailedReason.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_SetsFailedMetadata()
    {
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            "Body",
            null);

        notification.MarkFailed("push failed");

        notification.SendStatus.Should().Be(NotificationSendStatus.failed);
        notification.AttemptCount.Should().Be(1);
        notification.LastAttemptAt.Should().NotBeNull();
        notification.FailedReason.Should().Be("push failed");
    }
}
