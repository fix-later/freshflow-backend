using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class NotificationRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsNotificationAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var notification = new Notification(
            userId,
            NotificationType.credit_alert,
            "Credit alert",
            "Credit usage is high.",
            """{"level":"warning"}""");

        await sut.AddAsync(notification, default);

        var row = await db.Set<Notification>().SingleAsync();
        row.UserId.Should().Be(userId);
        row.Type.Should().Be(NotificationType.credit_alert);
        row.Payload.Should().Be("""{"level":"warning"}""");
        row.IsRead.Should().BeFalse();
        row.SendStatus.Should().Be(NotificationSendStatus.pending);
        row.AttemptCount.Should().Be(0);
        row.LastAttemptAt.Should().BeNull();
        row.FailedReason.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsSendStatusChangesAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var notification = await sut.AddAsync(new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            "System notification",
            "System notification body.",
            null), default);

        notification.MarkSent();
        await sut.UpdateAsync(notification, default);

        var row = await db.Set<Notification>().SingleAsync();
        row.SendStatus.Should().Be(NotificationSendStatus.sent);
        row.AttemptCount.Should().Be(1);
        row.LastAttemptAt.Should().NotBeNull();
        row.FailedReason.Should().BeNull();
    }

    [Fact]
    public async Task MarkReadAsync_ExistingUserNotification_SetsReadMetadataAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var notification = await sut.AddAsync(new Notification(
            userId,
            NotificationType.order_status,
            "Order confirmed",
            "Order confirmed.",
            null), default);

        var result = await sut.MarkReadAsync(userId, notification.Id, default);

        result.Should().NotBeNull();
        result!.IsRead.Should().BeTrue();
        result.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkReadAsync_DifferentUserNotification_ReturnsNullAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var notification = await sut.AddAsync(new Notification(
            ownerId,
            NotificationType.order_status,
            "Order confirmed",
            "Order confirmed.",
            null), default);

        var result = await sut.MarkReadAsync(otherUserId, notification.Id, default);

        result.Should().BeNull();
        var row = await db.Set<Notification>().SingleAsync();
        row.IsRead.Should().BeFalse();
    }


    [Fact]
    public async Task GetPageAsync_MoreRowsThanPageSize_ReturnsPageAndNextCursorAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddNotificationAsync(sut, db, userId, "Newest", baseTime.AddSeconds(3));
        var middle = await AddNotificationAsync(sut, db, userId, "Middle", baseTime.AddSeconds(2));
        await AddNotificationAsync(sut, db, userId, "Oldest", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(userId, null, 2, null, default);

        result.Items.Should().HaveCount(2);
        result.Items.Select(n => n.Id).Should().Equal(newest.Id, middle.Id);
        result.Items.Should().BeInDescendingOrder(n => n.CreatedAt);
        result.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPageAsync_RowsWithinPageSize_ReturnsNullNextCursorAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        await AddNotificationAsync(sut, db, userId, "Newest", baseTime.AddSeconds(2));
        await AddNotificationAsync(sut, db, userId, "Oldest", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(userId, null, 2, null, default);

        result.Items.Should().HaveCount(2);
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_WithCursorFromPreviousPage_ReturnsNextItemsInOrderAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddNotificationAsync(sut, db, userId, "Newest", baseTime.AddSeconds(3));
        var middle = await AddNotificationAsync(sut, db, userId, "Middle", baseTime.AddSeconds(2));
        await AddNotificationAsync(sut, db, userId, "Oldest", baseTime.AddSeconds(1));
        var firstPage = await sut.GetPageAsync(userId, null, 1, null, default);

        var secondPage = await sut.GetPageAsync(userId, firstPage.NextCursor, 1, null, default);

        firstPage.Items.Should().ContainSingle(n => n.Id == newest.Id);
        secondPage.Items.Should().ContainSingle(n => n.Id == middle.Id);
        secondPage.Items.Single().Id.Should().NotBe(firstPage.Items.Single().Id);
        secondPage.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPageAsync_IsReadFilterTrue_ReturnsOnlyReadNotificationsAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var read = await AddNotificationAsync(sut, db, userId, "Read", baseTime.AddSeconds(2));
        await sut.MarkReadAsync(userId, read.Id, default);
        await AddNotificationAsync(sut, db, userId, "Unread", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(userId, null, 10, true, default);

        result.Items.Should().ContainSingle(n => n.Id == read.Id);
        result.Items.Should().OnlyContain(n => n.IsRead);
    }

    [Fact]
    public async Task GetPageAsync_IsReadFilterFalse_ReturnsOnlyUnreadNotificationsAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var read = await AddNotificationAsync(sut, db, userId, "Read", baseTime.AddSeconds(2));
        await sut.MarkReadAsync(userId, read.Id, default);
        var unread = await AddNotificationAsync(sut, db, userId, "Unread", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(userId, null, 10, false, default);

        result.Items.Should().ContainSingle(n => n.Id == unread.Id);
        result.Items.Should().OnlyContain(n => !n.IsRead);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageAsync_PageSizeZeroOrNegative_ThrowsArgumentExceptionAsync(int pageSize)
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        Func<Task> act = () => sut.GetPageAsync(Guid.NewGuid(), null, pageSize, null, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPageAsync_MalformedCursor_IgnoresCursorAndReturnsFirstPageAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddNotificationAsync(sut, db, userId, "Newest", baseTime.AddSeconds(2));
        await AddNotificationAsync(sut, db, userId, "Oldest", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(userId, "not-valid-base64-or-json", 1, null, default);

        result.Items.Should().ContainSingle(n => n.Id == newest.Id);
        result.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPageAsync_DifferentUser_DoesNotReturnOtherUsersNotificationsAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var ownNotification = await AddNotificationAsync(sut, db, userId, "Own", baseTime.AddSeconds(2));
        await AddNotificationAsync(sut, db, otherUserId, "Other", baseTime.AddSeconds(3));

        var result = await sut.GetPageAsync(userId, null, 10, null, default);

        result.Items.Should().ContainSingle(n => n.Id == ownNotification.Id);
        result.Items.Should().OnlyContain(n => n.UserId == userId);
    }

    [Fact]
    public async Task GetRetryablePageAsync_ReturnsOnlyFailedDueNotificationsUnderMaxAttemptsAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var now = DateTime.UtcNow;
        var due = await AddFailedNotificationAsync(sut, db, "Due", now.AddMinutes(-5), attempts: 1);
        var dueWithoutAttemptAt = await AddFailedNotificationAsync(sut, db, "Due null", null, attempts: 1);
        await AddFailedNotificationAsync(sut, db, "Recent", now.AddSeconds(-10), attempts: 1);
        await AddFailedNotificationAsync(sut, db, "Exhausted", now.AddMinutes(-5), attempts: 3);
        await AddNotificationAsync(sut, db, Guid.NewGuid(), "Pending", now.AddMinutes(-5));
        var sent = await AddNotificationAsync(sut, db, Guid.NewGuid(), "Sent", now.AddMinutes(-5));
        sent.MarkSent();
        await sut.UpdateAsync(sent, default);

        var result = await sut.GetRetryablePageAsync(
            maxAttempts: 3,
            backoffThreshold: now.AddMinutes(-1),
            batchSize: 10,
            default);

        result.Select(n => n.Id).Should().BeEquivalentTo([due.Id, dueWithoutAttemptAt.Id]);
        result.Should().OnlyContain(n =>
            n.SendStatus == NotificationSendStatus.failed &&
            n.AttemptCount < 3 &&
            (n.LastAttemptAt == null || n.LastAttemptAt < now.AddMinutes(-1)));
    }

    [Fact]
    public async Task GetRetryablePageAsync_RespectsBatchSizeAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        var now = DateTime.UtcNow;
        await AddFailedNotificationAsync(sut, db, "First", now.AddMinutes(-5), attempts: 1);
        await AddFailedNotificationAsync(sut, db, "Second", now.AddMinutes(-4), attempts: 1);

        var result = await sut.GetRetryablePageAsync(
            maxAttempts: 3,
            backoffThreshold: now.AddMinutes(-1),
            batchSize: 1,
            default);

        result.Should().ContainSingle();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(3, 0)]
    [InlineData(3, -1)]
    public async Task GetRetryablePageAsync_InvalidLimits_ThrowsArgumentExceptionAsync(
        int maxAttempts,
        int batchSize)
    {
        using var db = CreateContext();
        var sut = new NotificationRepository(db);
        Func<Task> act = () => sut.GetRetryablePageAsync(
            maxAttempts,
            DateTime.UtcNow,
            batchSize,
            default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notifications-{Guid.NewGuid()}")
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
            null), default);

        db.Entry(notification).Property(n => n.CreatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return notification;
    }

    private static async Task<Notification> AddFailedNotificationAsync(
        NotificationRepository repository,
        AppDbContext db,
        string title,
        DateTime? lastAttemptAt,
        int attempts)
    {
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.system,
            title,
            $"{title} body.",
            null);

        for (var i = 0; i < attempts; i++)
            notification.MarkFailed("provider down");

        await repository.AddAsync(notification, default);
        db.Entry(notification).Property(n => n.LastAttemptAt).CurrentValue = lastAttemptAt;
        await db.SaveChangesAsync();

        return notification;
    }
}
