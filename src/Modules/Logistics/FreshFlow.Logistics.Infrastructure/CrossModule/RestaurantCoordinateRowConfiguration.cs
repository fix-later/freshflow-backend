using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateRowConfiguration : IEntityTypeConfiguration<RestaurantCoordinateRow>
{
    public void Configure(EntityTypeBuilder<RestaurantCoordinateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "RestaurantId", "Latitude", "Longitude" FROM delivery_addresses WHERE "IsDefault" = true AND "DeletedAt" IS NULL""");
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.Latitude);
        builder.Property(r => r.Longitude);
    }
}
