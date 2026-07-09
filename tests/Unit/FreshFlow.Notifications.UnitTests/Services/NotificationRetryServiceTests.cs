using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Services;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Repositories;
using FreshFlow.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class NotificationRetryServiceTests
{
    [Fact]
    public async Task RetryDueAsync_FailureThenSuccess_IncrementsAttemptsAndMarksSentAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var notification = CreateNotification();
        notification.MarkFailed("initial failure");
        await repository.AddAsync(notification, default);
        var pushSender = new SequencePushSender(
            Result.Failure(Error.Validation("PUSH_FAILED", "still down")),
            Result.Success());
        var sut = new NotificationRetryService(repository, pushSender);

        var firstProcessed = await sut.RetryDueAsync(
            maxAttempts: 5,
            backoff: TimeSpan.Zero,
            batchSize: 10,
            default);
        var secondProcessed = await sut.RetryDueAsync(
            maxAttempts: 5,
            backoff: TimeSpan.Zero,
            batchSize: 10,
            default);

        firstProcessed.Should().Be(1);
        secondProcessed.Should().Be(1);
        var row = await db.Set<Notification>().SingleAsync();
        row.SendStatus.Should().Be(NotificationSendStatus.sent);
        row.AttemptCount.Should().Be(3);
        row.LastAttemptAt.Should().NotBeNull();
        row.FailedReason.Should().BeNull();
        pushSender.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryDueAsync_MaxAttemptsReached_StopsRetryingNotificationAsync()
    {
        using var db = CreateContext();
        var repository = new NotificationRepository(db);
        var notification = CreateNotification();
        notification.MarkFailed("initial failure");
        await repository.AddAsync(notification, default);
        var pushSender = new SequencePushSender(
            Result.Failure(Error.Validation("PUSH_FAILED", "still down")));
        var sut = new NotificationRetryService(repository, pushSender);

        var firstProcessed = await sut.RetryDueAsync(
            maxAttempts: 3,
            backoff: TimeSpan.Zero,
            batchSize: 10,
            default);
        var secondProcessed = await sut.RetryDueAsync(
            maxAttempts: 3,
            backoff: TimeSpan.Zero,
            batchSize: 10,
            default);
        var thirdProcessed = await sut.RetryDueAsync(
            maxAttempts: 3,
            backoff: TimeSpan.Zero,
            batchSize: 10,
            default);

        firstProcessed.Should().Be(1);
        secondProcessed.Should().Be(1);
        thirdProcessed.Should().Be(0);
        var row = await db.Set<Notification>().SingleAsync();
        row.SendStatus.Should().Be(NotificationSendStatus.failed);
        row.AttemptCount.Should().Be(3);
        row.FailedReason.Should().Be("still down");
        pushSender.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryDueAsync_PushThrows_MarksFailedAndContinuesBatchAsync()
    {
        var repository = new InMemoryNotificationRepository();
        var first = CreateNotification();
        first.MarkFailed("first failure");
        var second = CreateNotification();
        second.MarkFailed("second failure");
        repository.Retryable.AddRange([first, second]);
        var pushSender = new ThrowThenSuccessPushSender();
        var sut = new NotificationRetryService(repository, pushSender);

        var processed = await sut.RetryDueAsync(
            maxAttempts: 5,
            backoff: TimeSpan.Zero,
            batchSize: 10,
            default);

        processed.Should().Be(2);
        first.SendStatus.Should().Be(NotificationSendStatus.failed);
        first.AttemptCount.Should().Be(2);
        first.FailedReason.Should().Be("provider crash");
        second.SendStatus.Should().Be(NotificationSendStatus.sent);
        second.AttemptCount.Should().Be(2);
        repository.UpdateCount.Should().Be(2);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notification-retry-service-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static Notification CreateNotification() =>
        new(
            Guid.NewGuid(),
            NotificationType.system,
            "Notification",
            "Notification body.",
            null);

    private sealed class SequencePushSender(params Result[] results) : IPushSender
    {
        private int _index;

        public int CallCount { get; private set; }

        public Task<Result> SendAsync(Notification notification, CancellationToken ct)
        {
            CallCount++;
            var result = _index < results.Length ? results[_index++] : results[^1];
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowThenSuccessPushSender : IPushSender
    {
        private int _callCount;

        public Task<Result> SendAsync(Notification notification, CancellationToken ct)
        {
            _callCount++;
            return _callCount == 1
                ? Task.FromException<Result>(new InvalidOperationException("provider crash"))
                : Task.FromResult(Result.Success());
        }
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<Notification> Retryable { get; } = [];
        public int UpdateCount { get; private set; }

        public Task<Notification> AddAsync(Notification notification, CancellationToken ct) =>
            Task.FromResult(notification);

        public Task UpdateAsync(Notification notification, CancellationToken ct)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Notification> Items, string? NextCursor)> GetPageAsync(
            Guid userId,
            string? cursor,
            int pageSize,
            bool? isRead,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Notification?> FindByIdForUserAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Notification?> MarkReadAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Notification>> GetRetryablePageAsync(
            int maxAttempts,
            DateTime backoffThreshold,
            int batchSize,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Notification>>(Retryable.AsReadOnly());
    }
}
