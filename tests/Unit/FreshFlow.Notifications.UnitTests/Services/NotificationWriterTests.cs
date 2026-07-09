using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Services;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class NotificationWriterTests
{
    [Fact]
    public async Task WriteAsync_SerializesPayloadAndPersistsNotificationAsync()
    {
        var repository = Substitute.For<INotificationRepository>();
        Notification? captured = null;
        repository.AddAsync(Arg.Do<Notification>(n => captured = n), default)
            .Returns(call => (Notification)call[0]!);
        var sut = new NotificationWriter(repository);
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var notification = await sut.WriteAsync(
            userId,
            NotificationType.order_status,
            " Title ",
            " Body ",
            new Dictionary<string, object?>
            {
                ["order_id"] = orderId,
                ["new_status"] = "confirmed",
                ["empty_value"] = null,
            },
            default);

        notification.Should().BeSameAs(captured);
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.Type.Should().Be(NotificationType.order_status);
        captured.Title.Should().Be("Title");
        captured.Body.Should().Be("Body");
        captured.Payload.Should().Contain("\"order_id\"");
        captured.Payload.Should().Contain(orderId.ToString());
        captured.Payload.Should().Contain("\"new_status\":\"confirmed\"");
        captured.Payload.Should().NotContain("empty_value");
        captured.IsRead.Should().BeFalse();
        captured.ReadAt.Should().BeNull();
    }
}
