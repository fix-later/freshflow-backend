using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateRowConfiguration
    : IEntityTypeConfiguration<RestaurantCoordinateRow>
{
    public void Configure(EntityTypeBuilder<RestaurantCoordinateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT da."RestaurantId", r."Name" AS "Name", da."Latitude", da."Longitude"
            FROM delivery_addresses da
            JOIN restaurants r ON r."Id" = da."RestaurantId"
            WHERE da."IsDefault" = true AND da."DeletedAt" IS NULL AND r.status = 'active'
            """);
        builder.Property(row => row.RestaurantId);
        builder.Property(row => row.Name);
        builder.Property(row => row.Latitude);
        builder.Property(row => row.Longitude);
    }
}
