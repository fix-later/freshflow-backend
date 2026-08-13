using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketSessionVehicleRowConfiguration : IEntityTypeConfiguration<MarketSessionVehicleRow>
{
    public void Configure(EntityTypeBuilder<MarketSessionVehicleRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery("""
            SELECT id AS "Id", hub_id AS "HubId", capacity_kg AS "CapacityKg",
                   plate_number AS "PlateNumber", vehicle_type AS "VehicleType",
                   is_available AS "IsAvailable", deleted_at AS "DeletedAt"
            FROM vehicles
            """);
    }
}

internal sealed class ReservedRouteVehicleRowConfiguration : IEntityTypeConfiguration<ReservedRouteVehicleRow>
{
    public void Configure(EntityTypeBuilder<ReservedRouteVehicleRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery("""
            SELECT vehicle_id AS "VehicleId", service_date AS "ServiceDate"
            FROM delivery_routes
            WHERE vehicle_id IS NOT NULL AND status <> 'cancelled' AND deleted_at IS NULL
            """);
    }
}
