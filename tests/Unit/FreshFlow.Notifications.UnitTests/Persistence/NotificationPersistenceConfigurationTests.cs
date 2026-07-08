using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Infrastructure;
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
    public void AddNotificationsModule_RegistersNotificationDeviceRepository()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"notifications-{Guid.NewGuid()}"));

        services.AddNotificationsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<INotificationDeviceRepository>().Should().NotBeNull();
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
