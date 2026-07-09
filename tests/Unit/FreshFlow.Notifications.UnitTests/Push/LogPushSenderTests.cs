using FluentAssertions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreshFlow.Notifications.UnitTests.Push;

[Trait("Category", "Unit")]
public sealed class LogPushSenderTests
{
    [Fact]
    public async Task SendAsync_ReturnsSuccessAsync()
    {
        var sut = new LogPushSender(NullLogger<LogPushSender>.Instance);
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            "System notification",
            "System notification body.",
            null);

        var result = await sut.SendAsync(notification, default);

        result.IsSuccess.Should().BeTrue();
    }
}
