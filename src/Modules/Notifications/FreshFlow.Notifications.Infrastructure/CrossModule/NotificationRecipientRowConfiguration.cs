using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Notifications.Infrastructure.CrossModule;

internal sealed class NotificationRecipientRowConfiguration : IEntityTypeConfiguration<NotificationRecipientRow>
{
    public void Configure(EntityTypeBuilder<NotificationRecipientRow> builder)
    {
        // Read-only projection onto restaurants (Auth module).
        // ToSqlQuery avoids a model conflict with Auth's ToTable("restaurants").
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id" AS "RestaurantId", "UserId" FROM restaurants""");
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.UserId);
    }
}
