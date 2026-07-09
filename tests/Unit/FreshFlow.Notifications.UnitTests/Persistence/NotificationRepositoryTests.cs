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
}
