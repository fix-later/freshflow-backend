using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Application.Services;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class NotificationWriterTests
{
    [Fact]
    public async Task WriteAsync_SerializesPayloadAndPersistsNotificationAsync()
    {
        var repository = Substitute.For<INotificationRepository>();
        var pushSender = Substitute.For<IPushSender>();
        var broadcast = Substitute.For<INotificationBroadcastService>();
        Notification? captured = null;
        repository.AddAsync(Arg.Do<Notification>(n => captured = n), default)
            .Returns(call => (Notification)call[0]!);
        repository.UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        pushSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        var sut = new NotificationWriter(
            repository, pushSender, broadcast, NullLogger<NotificationWriter>.Instance);
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

    [Fact]
    public async Task WriteAsync_PushSuccess_MarksSentAndPersistsUpdateAsync()
    {
        var repository = Substitute.For<INotificationRepository>();
        var pushSender = Substitute.For<IPushSender>();
        var broadcast = Substitute.For<INotificationBroadcastService>();
        NotificationSendStatus? statusAtPush = null;
        int? attemptCountAtPush = null;
        DateTime? lastAttemptAtPush = null;
        string? failedReasonAtPush = "not captured";
        repository.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => (Notification)call[0]!);
        repository.UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        pushSender.SendAsync(
                Arg.Do<Notification>(notification =>
                {
                    statusAtPush = notification.SendStatus;
                    attemptCountAtPush = notification.AttemptCount;
                    lastAttemptAtPush = notification.LastAttemptAt;
                    failedReasonAtPush = notification.FailedReason;
                }),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        var sut = new NotificationWriter(
            repository, pushSender, broadcast, NullLogger<NotificationWriter>.Instance);

        var result = await sut.WriteAsync(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            "Body",
            null,
            default);

        statusAtPush.Should().Be(NotificationSendStatus.pending);
        attemptCountAtPush.Should().Be(0);
        lastAttemptAtPush.Should().BeNull();
        failedReasonAtPush.Should().BeNull();
        result.SendStatus.Should().Be(NotificationSendStatus.sent);
        result.AttemptCount.Should().Be(1);
        result.LastAttemptAt.Should().NotBeNull();
        result.FailedReason.Should().BeNull();
        await repository.Received(1).UpdateAsync(result, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_PushFailure_MarksFailedAndReturnsPersistedNotificationAsync()
    {
        var repository = Substitute.For<INotificationRepository>();
        var pushSender = Substitute.For<IPushSender>();
        var broadcast = Substitute.For<INotificationBroadcastService>();
        repository.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => (Notification)call[0]!);
        repository.UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        pushSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure(Error.Validation("PUSH_FAILED", "provider down"))));
        var sut = new NotificationWriter(
            repository, pushSender, broadcast, NullLogger<NotificationWriter>.Instance);

        var result = await sut.WriteAsync(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            "Body",
            null,
            default);

        result.SendStatus.Should().Be(NotificationSendStatus.failed);
        result.AttemptCount.Should().Be(1);
        result.LastAttemptAt.Should().NotBeNull();
        result.FailedReason.Should().Be("provider down");
        await repository.Received(1).UpdateAsync(result, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_PushThrows_MarksFailedAndDoesNotThrowAsync()
    {
        var repository = Substitute.For<INotificationRepository>();
        var pushSender = Substitute.For<IPushSender>();
        var broadcast = Substitute.For<INotificationBroadcastService>();
        repository.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => (Notification)call[0]!);
        repository.UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        pushSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result>(new InvalidOperationException("push exploded")));
        var sut = new NotificationWriter(
            repository, pushSender, broadcast, NullLogger<NotificationWriter>.Instance);

        var result = await sut.WriteAsync(
            Guid.NewGuid(),
            NotificationType.system,
            "Title",
            "Body",
            null,
            default);

        result.SendStatus.Should().Be(NotificationSendStatus.failed);
        result.AttemptCount.Should().Be(1);
        result.LastAttemptAt.Should().NotBeNull();
        result.FailedReason.Should().Be("push exploded");
        await repository.Received(1).UpdateAsync(result, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_PersistsThenBroadcastsThenPushesAsync()
    {
        var calls = new List<string>();
        var repository = Substitute.For<INotificationRepository>();
        var pushSender = Substitute.For<IPushSender>();
        var broadcast = Substitute.For<INotificationBroadcastService>();
        repository.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                calls.Add("persist");
                return (Notification)call[0]!;
            });
        broadcast.BroadcastCreatedAsync(
                Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("broadcast");
                return Task.CompletedTask;
            });
        pushSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("push");
                return Task.FromResult(Result.Success());
            });
        repository.UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls.Add("update");
                return Task.CompletedTask;
            });
        var sut = new NotificationWriter(
            repository, pushSender, broadcast, NullLogger<NotificationWriter>.Instance);
        var userId = Guid.NewGuid();

        var result = await sut.WriteAsync(
            userId, NotificationType.system, "Title", "Body", null, default);

        calls.Should().Equal("persist", "broadcast", "push", "update");
        await broadcast.Received(1).BroadcastCreatedAsync(
            userId,
            Arg.Is<NotificationDto>(dto => dto.Id == result.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_BroadcastFailure_DoesNotBlockPushAsync()
    {
        var repository = Substitute.For<INotificationRepository>();
        var pushSender = Substitute.For<IPushSender>();
        var broadcast = Substitute.For<INotificationBroadcastService>();
        repository.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(call => (Notification)call[0]!);
        repository.UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        broadcast.BroadcastCreatedAsync(
                Arg.Any<Guid>(), Arg.Any<NotificationDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("signalr unavailable")));
        pushSender.SendAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));
        var sut = new NotificationWriter(
            repository, pushSender, broadcast, NullLogger<NotificationWriter>.Instance);

        var result = await sut.WriteAsync(
            Guid.NewGuid(), NotificationType.system, "Title", "Body", null, default);

        result.SendStatus.Should().Be(NotificationSendStatus.sent);
        await pushSender.Received(1).SendAsync(result, Arg.Any<CancellationToken>());
    }
}
