using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Notifications.Infrastructure.CrossModule;

internal sealed class NotificationRecipientRowConfiguration : IEntityTypeConfiguration<NotificationRecipientRow>
{
    public void Configure(EntityTypeBuilder<NotificationRecipientRow> builder)
    {
        // Read-only projection onto restaurants + their owner user (Auth module).
        // ToSqlQuery avoids a model conflict with Auth's ToTable("restaurants")/ToTable("users").
        // Both tables use quoted PascalCase identifiers; users are soft-deleted, so a restaurant
        // whose owner user is deleted resolves to no row (and thus no recipient).
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT r."Id" AS "RestaurantId", r."UserId", u."Email"
            FROM restaurants r
            JOIN users u ON u."Id" = r."UserId"
            WHERE u."DeletedAt" IS NULL
            """);
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.UserId);
        builder.Property(r => r.Email);
    }
}
