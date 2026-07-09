using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Queries.ListNotifications;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListNotificationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyCallerNotificationsAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new ListNotificationsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);
        var own = await AddNotificationAsync(repository, db, userId, "Own", baseTime.AddSeconds(2));
        await AddNotificationAsync(repository, db, otherUserId, "Other", baseTime.AddSeconds(3));

        var result = await sut.Handle(new ListNotificationsQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(n => n.Id == own.Id);
        result.Value.Items.Should().OnlyContain(n => n.Id != own.Id || n.Title == "Own");
    }

    [Fact]
    public async Task Handle_WithCursor_ReturnsNextPageWithoutDuplicatesAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new ListNotificationsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddNotificationAsync(repository, db, userId, "Newest", baseTime.AddSeconds(3));
        var middle = await AddNotificationAsync(repository, db, userId, "Middle", baseTime.AddSeconds(2));
        var oldest = await AddNotificationAsync(repository, db, userId, "Oldest", baseTime.AddSeconds(1));

        var firstPage = await sut.Handle(new ListNotificationsQuery(userId, PageSize: 2), default);
        var secondPage = await sut.Handle(
            new ListNotificationsQuery(userId, firstPage.Value.NextCursor, PageSize: 2),
            default);

        firstPage.Value.Items.Select(n => n.Id).Should().Equal(newest.Id, middle.Id);
        firstPage.Value.NextCursor.Should().NotBeNull();
        secondPage.Value.Items.Should().ContainSingle(n => n.Id == oldest.Id);
        secondPage.Value.Items.Should().NotContain(n => firstPage.Value.Items.Select(i => i.Id).Contains(n.Id));
        secondPage.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_IsReadTrue_ReturnsOnlyReadNotificationsAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new ListNotificationsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var read = await AddNotificationAsync(repository, db, userId, "Read", DateTime.UtcNow.AddSeconds(1));
        await repository.MarkReadAsync(userId, read.Id, default);
        await AddNotificationAsync(repository, db, userId, "Unread", DateTime.UtcNow);

        var result = await sut.Handle(new ListNotificationsQuery(userId, IsRead: true), default);

        result.Value.Items.Should().ContainSingle(n => n.Id == read.Id);
        result.Value.Items.Should().OnlyContain(n => n.IsRead);
    }

    [Fact]
    public async Task Handle_IsReadFalse_ReturnsOnlyUnreadNotificationsAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new ListNotificationsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var read = await AddNotificationAsync(repository, db, userId, "Read", DateTime.UtcNow.AddSeconds(1));
        await repository.MarkReadAsync(userId, read.Id, default);
        var unread = await AddNotificationAsync(repository, db, userId, "Unread", DateTime.UtcNow);

        var result = await sut.Handle(new ListNotificationsQuery(userId, IsRead: false), default);

        result.Value.Items.Should().ContainSingle(n => n.Id == unread.Id);
        result.Value.Items.Should().OnlyContain(n => !n.IsRead);
    }

    [Fact]
    public async Task Handle_IsReadNull_ReturnsReadAndUnreadNotificationsAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var sut = new ListNotificationsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var read = await AddNotificationAsync(repository, db, userId, "Read", DateTime.UtcNow.AddSeconds(1));
        await repository.MarkReadAsync(userId, read.Id, default);
        var unread = await AddNotificationAsync(repository, db, userId, "Unread", DateTime.UtcNow);

        var result = await sut.Handle(new ListNotificationsQuery(userId, IsRead: null), default);

        result.Value.Items.Select(n => n.Id).Should().BeEquivalentTo([read.Id, unread.Id]);
    }

    [Fact]
    public void Query_DefaultPageSize_Is50()
    {
        var query = new ListNotificationsQuery(Guid.NewGuid());

        query.PageSize.Should().Be(50);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notifications-list-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Notification> AddNotificationAsync(
        NotificationRepository repository,
        AppDbContext db,
        Guid userId,
        string title,
        DateTime createdAt)
    {
        var notification = await repository.AddAsync(new Notification(
            userId,
            NotificationType.order_status,
            title,
            $"{title} body.",
            """{"order_id":"test"}"""), default);

        db.Entry(notification).Property(n => n.CreatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return notification;
    }
}
