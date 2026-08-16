using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Commands.MarkNotificationRead;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class MarkNotificationReadCommandHandlerTests
{
    [Fact]
    public async Task Handle_UnreadNotification_MarksReadAndReturnsDtoAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new MarkNotificationReadCommandHandler(repository);
        var userId = Guid.NewGuid();
        var notification = await repository.AddAsync(CreateNotification(userId), default);

        var result = await sut.Handle(
            new MarkNotificationReadCommand(userId, notification.Id),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(notification.Id);
        result.Value.Type.Should().Be("credit_alert");
        result.Value.IsRead.Should().BeTrue();
        result.Value.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AlreadyReadNotification_IsIdempotentAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new MarkNotificationReadCommandHandler(repository);
        var userId = Guid.NewGuid();
        var notification = await repository.AddAsync(CreateNotification(userId), default);

        var first = await sut.Handle(new MarkNotificationReadCommand(userId, notification.Id), default);
        var second = await sut.Handle(new MarkNotificationReadCommand(userId, notification.Id), default);

        second.IsSuccess.Should().BeTrue();
        second.Value.IsRead.Should().BeTrue();
        second.Value.ReadAt.Should().Be(first.Value.ReadAt);
    }

    [Fact]
    public async Task Handle_MissingNotification_ReturnsNotFoundAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new MarkNotificationReadCommandHandler(repository);
        var notificationId = Guid.NewGuid();

        var result = await sut.Handle(
            new MarkNotificationReadCommand(Guid.NewGuid(), notificationId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NOTIFICATION_NOT_FOUND");
        result.Error.Message.Should().Contain(notificationId.ToString());
    }

    [Fact]
    public async Task Handle_OtherUsersNotification_ReturnsNotFoundAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new MarkNotificationReadCommandHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var notification = await repository.AddAsync(CreateNotification(ownerId), default);

        var result = await sut.Handle(
            new MarkNotificationReadCommand(otherUserId, notification.Id),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NOTIFICATION_NOT_FOUND");
        result.Error.Message.Should().Contain(notification.Id.ToString());
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notifications-mark-read-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static Notification CreateNotification(Guid userId) =>
        new(
            userId,
            NotificationType.credit_alert,
            "Credit alert",
            "Credit usage is high.",
            """{"level":"warning"}""");
}
