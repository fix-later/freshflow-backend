using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Infrastructure;
using FreshFlow.Notifications.Infrastructure.CrossModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Notifications.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class NotificationPersistenceConfigurationTests
{
    [Fact]
    public void Model_RegistersNotificationDeviceEntity()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(NotificationDevice));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("notification_devices");
    }

    [Fact]
    public void Model_RegistersNotificationEntity()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(Notification));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("notifications");
    }

    [Fact]
    public void NotificationDeviceConfiguration_UsesSnakeCaseColumnsAndActiveUniqueIndex()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(NotificationDevice))!;
        var table = StoreObjectIdentifier.Table("notification_devices", null);

        entity.FindProperty(nameof(NotificationDevice.UserId))!
            .GetColumnName(table)
            .Should().Be("user_id");
        entity.FindProperty(nameof(NotificationDevice.DeviceId))!
            .GetColumnName(table)
            .Should().Be("device_id");
        entity.FindProperty(nameof(NotificationDevice.RevokedAt))!
            .GetColumnName(table)
            .Should().Be("revoked_at");

        var activeTokenIndex = entity.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(NotificationDevice.UserId), nameof(NotificationDevice.Token)]));

        activeTokenIndex.IsUnique.Should().BeTrue();
        activeTokenIndex.GetFilter().Should().Be("revoked_at IS NULL");
        entity.GetForeignKeys().Should().BeEmpty(
            "notification_devices.user_id is a cross-module plain Guid per DEC-NOT-12");
    }

    [Fact]
    public void NotificationConfiguration_UsesSnakeCaseColumnsAndPlainUserId()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(Notification))!;
        var table = StoreObjectIdentifier.Table("notifications", null);

        entity.FindProperty(nameof(Notification.UserId))!
            .GetColumnName(table)
            .Should().Be("user_id");
        entity.FindProperty(nameof(Notification.IsRead))!
            .GetColumnName(table)
            .Should().Be("is_read");
        entity.FindProperty(nameof(Notification.ReadAt))!
            .GetColumnName(table)
            .Should().Be("read_at");
        entity.FindProperty(nameof(Notification.CreatedAt))!
            .GetColumnName(table)
            .Should().Be("created_at");

        entity.GetForeignKeys().Should().BeEmpty(
            "notifications.user_id is a cross-module plain Guid per DEC-NOT-12");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_notifications_user_id");
    }

    [Fact]
    public void Model_RegistersNotificationRecipientProjectionAsKeyless()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(NotificationRecipientRow));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetSqlQuery().Should().Contain("FROM restaurants");
    }

    [Fact]
    public void AddNotificationsModule_RegistersNotificationServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"notifications-{Guid.NewGuid()}"));

        services.AddNotificationsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<INotificationDeviceRepository>().Should().NotBeNull();
        provider.GetRequiredService<INotificationRepository>().Should().NotBeNull();
        provider.GetRequiredService<INotificationWriter>().Should().NotBeNull();
        provider.GetRequiredService<INotificationRecipientResolver>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Notifications.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"notifications-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}
