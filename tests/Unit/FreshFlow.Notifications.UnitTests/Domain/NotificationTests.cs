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
}
