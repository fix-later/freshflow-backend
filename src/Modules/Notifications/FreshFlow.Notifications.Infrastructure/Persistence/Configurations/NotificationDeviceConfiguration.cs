using FreshFlow.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationDeviceConfiguration : IEntityTypeConfiguration<NotificationDevice>
{
    public void Configure(EntityTypeBuilder<NotificationDevice> builder)
    {
        builder.ToTable("notification_devices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(d => d.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(d => d.Token)
            .HasColumnName("token")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(d => d.Platform)
            .HasColumnName("platform")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(d => d.DeviceId)
            .HasColumnName("device_id");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(d => d.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(d => d.UserId)
            .HasDatabaseName("idx_notification_devices_user_id");

        builder.HasIndex(d => new { d.UserId, d.Token })
            .IsUnique()
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName("ux_notification_devices_user_token_active");
    }
}
