using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Notifications.Infrastructure.CrossModule;

internal sealed class NotificationOrderRecipientRowConfiguration : IEntityTypeConfiguration<NotificationOrderRecipientRow>
{
    public void Configure(EntityTypeBuilder<NotificationOrderRecipientRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT o."Id" AS "OrderId", r."UserId" AS "UserId"
            FROM orders o
            JOIN restaurants r ON r."Id" = o."RestaurantId"
            WHERE o."deleted_at" IS NULL
            """);
        builder.Property(r => r.OrderId);
        builder.Property(r => r.UserId);
    }
}
