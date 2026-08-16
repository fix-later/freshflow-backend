using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FreshFlow.Notifications.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class NotificationDeviceRepositoryTests
{
    [Fact]
    public async Task RegisterAsync_NewToken_CreatesActiveDeviceAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();

        var device = await sut.RegisterAsync(
            userId,
            " push-token ",
            NotificationDevicePlatform.ios,
            " device-1 ",
            default);

        device.UserId.Should().Be(userId);
        device.Token.Should().Be("push-token");
        device.Platform.Should().Be(NotificationDevicePlatform.ios);
        device.DeviceId.Should().Be("device-1");
        device.RevokedAt.Should().BeNull();

        var rows = await db.Set<NotificationDevice>().ToListAsync();
        rows.Should().ContainSingle();
        rows[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_SameUserAndToken_UpdatesExistingWithoutDuplicateAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();

        var first = await sut.RegisterAsync(
            userId,
            "push-token",
            NotificationDevicePlatform.ios,
            "device-1",
            default);
        var second = await sut.RegisterAsync(
            userId,
            "push-token",
            NotificationDevicePlatform.android,
            "device-2",
            default);

        second.Id.Should().Be(first.Id);
        second.Platform.Should().Be(NotificationDevicePlatform.android);
        second.DeviceId.Should().Be("device-2");
        second.RevokedAt.Should().BeNull();

        var rows = await db.Set<NotificationDevice>().ToListAsync();
        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterAsync_RevokedToken_ReactivatesExistingDeviceAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();

        var first = await sut.RegisterAsync(
            userId,
            "push-token",
            NotificationDevicePlatform.web,
            null,
            default);
        await sut.UnregisterAsync(userId, "push-token", default);

        var second = await sut.RegisterAsync(
            userId,
            "push-token",
            NotificationDevicePlatform.ios,
            "device-ios",
            default);

        second.Id.Should().Be(first.Id);
        second.RevokedAt.Should().BeNull();
        second.Platform.Should().Be(NotificationDevicePlatform.ios);
        second.DeviceId.Should().Be("device-ios");

        var rows = await db.Set<NotificationDevice>().ToListAsync();
        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterAsync_SameTokenForAnotherUser_TransfersDeviceAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var first = await sut.RegisterAsync(
            userA, "push-token", NotificationDevicePlatform.ios, "device-1", default);
        var transferred = await sut.RegisterAsync(
            userB, "push-token", NotificationDevicePlatform.android, "device-1", default);

        transferred.Id.Should().Be(first.Id);
        transferred.UserId.Should().Be(userB);
        transferred.Platform.Should().Be(NotificationDevicePlatform.android);
        (await db.Set<NotificationDevice>().CountAsync(d => d.RevokedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_RotatedTokenForSameDevice_ReplacesTokenAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();

        var first = await sut.RegisterAsync(
            userId, "old-token", NotificationDevicePlatform.ios, "device-1", default);
        var rotated = await sut.RegisterAsync(
            userId, "new-token", NotificationDevicePlatform.ios, "device-1", default);

        rotated.Id.Should().Be(first.Id);
        rotated.Token.Should().Be("new-token");
        (await db.Set<NotificationDevice>().CountAsync(d => d.RevokedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_TokenAndDeviceBelongToDifferentRows_RevokesConflictAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();
        var tokenOwner = await sut.RegisterAsync(
            userId, "token-1", NotificationDevicePlatform.ios, "device-1", default);
        var deviceOwner = await sut.RegisterAsync(
            userId, "token-2", NotificationDevicePlatform.ios, "device-2", default);

        var result = await sut.RegisterAsync(
            userId, "token-1", NotificationDevicePlatform.android, "device-2", default);

        result.Id.Should().Be(tokenOwner.Id);
        result.DeviceId.Should().Be("device-2");
        deviceOwner.RevokedAt.Should().NotBeNull();
        (await db.Set<NotificationDevice>().CountAsync(d => d.RevokedAt == null)).Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_UniqueViolationOnInsert_RequeriesAndReactivatesAsync()
    {
        var databaseName = $"notifications-race-{Guid.NewGuid()}";
        var root = new InMemoryDatabaseRoot();
        using var db = CreateContext(
            databaseName,
            root,
            new CompetingInsertUniqueViolationInterceptor(databaseName, root));
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();

        var device = await sut.RegisterAsync(
            userId,
            "push-token",
            NotificationDevicePlatform.ios,
            "device-ios",
            default);

        device.UserId.Should().Be(userId);
        device.Token.Should().Be("push-token");
        device.Platform.Should().Be(NotificationDevicePlatform.ios);
        device.DeviceId.Should().Be("device-ios");
        device.RevokedAt.Should().BeNull();

        using var verification = CreateContext(databaseName, root);
        var rows = await verification.Set<NotificationDevice>().ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(device.Id);
    }

    [Fact]
    public async Task UnregisterAsync_ExistingToken_SetsRevokedAtAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();
        await sut.RegisterAsync(userId, "push-token", NotificationDevicePlatform.android, null, default);

        var revoked = await sut.UnregisterAsync(userId, "push-token", default);

        revoked.Should().NotBeNull();
        revoked!.RevokedAt.Should().NotBeNull();

        var row = await db.Set<NotificationDevice>().SingleAsync();
        row.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UnregisterAsync_DifferentUserToken_DoesNotRevokeOtherUsersDeviceAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await sut.RegisterAsync(userB, "push-token", NotificationDevicePlatform.web, null, default);

        var result = await sut.UnregisterAsync(userA, "push-token", default);

        result.Should().BeNull();
        var row = await db.Set<NotificationDevice>().SingleAsync();
        row.UserId.Should().Be(userB);
        row.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveMobileAsync_ReturnsOnlyActiveIosAndAndroidDevicesAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);
        var userId = Guid.NewGuid();
        await sut.RegisterAsync(userId, "ios-token", NotificationDevicePlatform.ios, "ios", default);
        await sut.RegisterAsync(userId, "android-token", NotificationDevicePlatform.android, "android", default);
        await sut.RegisterAsync(userId, "web-token", NotificationDevicePlatform.web, "web", default);
        await sut.RevokeTokenAsync("android-token", default);

        var devices = await sut.GetActiveMobileAsync(userId, default);

        devices.Select(device => device.Token).Should().Equal("ios-token");
    }

    [Fact]
    public async Task RevokeTokenAsync_MissingToken_IsIdempotentAsync()
    {
        using var db = CreateContext();
        var sut = new NotificationDeviceRepository(db);

        var action = () => sut.RevokeTokenAsync("missing-token", default);

        await action.Should().NotThrowAsync();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notifications-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static AppDbContext CreateContext(
        string databaseName,
        InMemoryDatabaseRoot root,
        IInterceptor? interceptor = null)
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName, root);

        if (interceptor is not null)
            builder.AddInterceptors(interceptor);

        return new AppDbContext(builder.Options);
    }

    private sealed class CompetingInsertUniqueViolationInterceptor(
        string databaseName,
        InMemoryDatabaseRoot root) : SaveChangesInterceptor
    {
        private bool _hasThrown;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_hasThrown || eventData.Context is null)
                return result;

            var pending = eventData.Context.ChangeTracker
                .Entries<NotificationDevice>()
                .SingleOrDefault(e => e.State == EntityState.Added)
                ?.Entity;

            if (pending is null)
                return result;

            _hasThrown = true;

            using var competing = CreateContext(databaseName, root);
            var existing = new NotificationDevice(
                pending.UserId,
                pending.Token,
                NotificationDevicePlatform.web,
                "competing-device");
            await competing.Set<NotificationDevice>().AddAsync(existing, cancellationToken);
            await competing.SaveChangesAsync(cancellationToken);

            throw new DbUpdateException(
                "Simulated concurrent unique constraint violation.",
                new PostgresException(
                    messageText: "duplicate key value violates unique constraint",
                    severity: "ERROR",
                    invariantSeverity: "ERROR",
                    sqlState: PostgresErrorCodes.UniqueViolation));
        }
    }
}
