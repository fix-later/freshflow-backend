using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class RestaurantRowConfiguration : IEntityTypeConfiguration<RestaurantRow>
{
    public void Configure(EntityTypeBuilder<RestaurantRow> builder)
    {
        // Read-only projection onto restaurants (Auth module).
        // ToSqlQuery avoids a model conflict with Auth's ToTable("restaurants").
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id", "UserId", status AS "Status" FROM restaurants""");
        builder.Property(r => r.Id);
        builder.Property(r => r.UserId);
        builder.Property(r => r.Status);
    }
}

internal sealed class DeliveryAddressRowConfiguration : IEntityTypeConfiguration<DeliveryAddressRow>
{
    public void Configure(EntityTypeBuilder<DeliveryAddressRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT "Id", "RestaurantId", "RecipientName", "Phone", "AddressLine", "Latitude", "Longitude"
            FROM delivery_addresses
            WHERE "DeletedAt" IS NULL
            """);
        builder.Property(a => a.Id);
        builder.Property(a => a.RestaurantId);
        builder.Property(a => a.RecipientName);
        builder.Property(a => a.Phone);
        builder.Property(a => a.AddressLine);
        builder.Property(a => a.Latitude);
        builder.Property(a => a.Longitude);
    }
}
