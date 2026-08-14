using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class RestaurantCoordinateRowConfiguration : IEntityTypeConfiguration<RestaurantCoordinateRow>
{
    public void Configure(EntityTypeBuilder<RestaurantCoordinateRow> builder)
    {
        builder.HasNoKey();
        // Stops are located at the order's checkout address, so callers only need the restaurant's
        // display name here; the default-address coordinates are a fallback. Drive from restaurants and
        // LEFT JOIN the default address so an active restaurant without a live default address still
        // resolves its name (coordinates come back null) instead of vanishing from route planning.
        builder.ToSqlQuery(
            """
            SELECT r."Id" AS "RestaurantId", r."Name" AS "Name", da."Latitude", da."Longitude"
            FROM restaurants r
            LEFT JOIN delivery_addresses da
              ON da."RestaurantId" = r."Id" AND da."IsDefault" = true AND da."DeletedAt" IS NULL
            WHERE r.status = 'active'
            """);
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.Name);
        builder.Property(r => r.Latitude);
        builder.Property(r => r.Longitude);
    }
}
