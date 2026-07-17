using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class DeliveryRouteVehicleRowConfiguration
    : IEntityTypeConfiguration<DeliveryRouteVehicleRow>
{
    public void Configure(EntityTypeBuilder<DeliveryRouteVehicleRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                dr.id AS "DeliveryRouteId",
                v.capacity_kg AS "CapacityKg"
            FROM delivery_routes dr
            JOIN vehicles v ON v.id = dr.vehicle_id
            WHERE dr.deleted_at IS NULL
              AND v.deleted_at IS NULL
            """);
    }
}
