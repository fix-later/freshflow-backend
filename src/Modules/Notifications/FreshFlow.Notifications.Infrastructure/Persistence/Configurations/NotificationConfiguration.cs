using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(n => n.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.Body)
            .HasColumnName("body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb");

        builder.Property(n => n.IsRead)
            .HasColumnName("is_read")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(n => n.ReadAt)
            .HasColumnName("read_at");

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(n => n.SendStatus)
            .HasColumnName("send_status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(NotificationSendStatus.pending)
            .IsRequired();

        builder.Property(n => n.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(n => n.LastAttemptAt)
            .HasColumnName("last_attempt_at");

        builder.Property(n => n.FailedReason)
            .HasColumnName("failed_reason")
            .HasColumnType("text");

        builder.Property(n => n.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(n => n.UserId)
            .HasDatabaseName("idx_notifications_user_id");

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt, n.Id })
            .HasDatabaseName("idx_notifications_user_read_created");

        builder.HasIndex(n => new { n.AttemptCount, n.LastAttemptAt })
            .HasFilter("send_status = 'failed'")
            .HasDatabaseName("idx_notifications_retry_scan");
    }
}
