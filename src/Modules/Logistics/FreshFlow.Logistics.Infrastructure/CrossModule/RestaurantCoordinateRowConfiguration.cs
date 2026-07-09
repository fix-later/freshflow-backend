using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateRowConfiguration : IEntityTypeConfiguration<RestaurantCoordinateRow>
{
    public void Configure(EntityTypeBuilder<RestaurantCoordinateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT da."RestaurantId", r."Name" AS "Name", da."Latitude", da."Longitude"
            FROM delivery_addresses da
            JOIN restaurants r ON r."Id" = da."RestaurantId"
            WHERE da."IsDefault" = true AND da."DeletedAt" IS NULL
            """);
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.Name);
        builder.Property(r => r.Latitude);
        builder.Property(r => r.Longitude);
    }
}
