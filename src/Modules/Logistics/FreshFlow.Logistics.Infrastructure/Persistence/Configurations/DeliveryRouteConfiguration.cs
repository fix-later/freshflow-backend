using System.Text.Json;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryRouteConfiguration : IEntityTypeConfiguration<DeliveryRoute>
{
    public void Configure(EntityTypeBuilder<DeliveryRoute> builder)
    {
        builder.ToTable("delivery_routes");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(r => r.HubId)
            .HasColumnName("hub_id");
        builder.Property(r => r.MarketSessionId)
            .HasColumnName("market_session_id");


        builder.Property(r => r.RouteType)
            .HasColumnName("route_type")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.ServiceDate)
            .HasColumnName("service_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(r => r.TotalDistanceKm)
            .HasColumnName("total_distance_km")
            .HasColumnType("numeric(10,2)");

        builder.Property(r => r.EstimatedDurationMinutes)
            .HasColumnName("estimated_duration_minutes");

        builder.Property(r => r.EstimatedCost)
            .HasColumnName("estimated_cost")
            .HasColumnType("numeric(12,2)");

        builder.Property(r => r.OptimizationCriteria)
            .HasColumnName("optimization_criteria")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.VehicleId)
            .HasColumnName("vehicle_id");

        builder.Property(r => r.RoutePlanId).HasColumnName("route_plan_id");
        builder.Property(r => r.SuggestedVehicleId).HasColumnName("suggested_vehicle_id");
        builder.Property(r => r.PlannedLoadKg).HasColumnName("planned_load_kg").HasColumnType("numeric(14,3)");
        builder.Property(r => r.RoutingProfile).HasColumnName("routing_profile").HasMaxLength(20);
        builder.Property(r => r.EstimatedReturnAt).HasColumnName("estimated_return_at");

        builder.Property(r => r.DriverUserId)
            .HasColumnName("driver_user_id");

        builder.Property(r => r.OrderGroupId)
            .HasColumnName("order_group_id");

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        var stopsComparer = new ValueComparer<IReadOnlyList<RouteStop>>(
            (a, b) => (a ?? Array.Empty<RouteStop>()).SequenceEqual(b ?? Array.Empty<RouteStop>()),
            v => (v ?? Array.Empty<RouteStop>())
                .Aggregate(0, (hash, stop) => HashCode.Combine(hash, stop.GetHashCode())),
            v => (IReadOnlyList<RouteStop>)(v ?? Array.Empty<RouteStop>()).ToList().AsReadOnly());

        builder.Property(r => r.Stops)
            .HasColumnName("route_metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<RouteStop>>(v, (JsonSerializerOptions?)null) ?? new List<RouteStop>())
            .Metadata.SetValueComparer(stopsComparer);

        builder.HasIndex(r => new { r.VehicleId, r.ServiceDate })
            .HasDatabaseName("idx_delivery_routes_vehicle_service_date");

        builder.HasIndex(
                r => new { r.VehicleId, r.ServiceDate },
                "ux_delivery_routes_vehicle_service_date_reserved")
            .IsUnique()
            .HasFilter("vehicle_id IS NOT NULL AND status IN ('reviewed', 'assigned', 'in_progress') AND deleted_at IS NULL")
            .HasDatabaseName("ux_delivery_routes_vehicle_service_date_reserved");

        builder.HasIndex(r => r.RoutePlanId).HasDatabaseName("idx_delivery_routes_route_plan_id");

        builder.HasOne<RoutePlan>()
            .WithMany()
            .HasForeignKey(r => r.RoutePlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_delivery_routes_route_plan");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("idx_delivery_routes_status");

        builder.HasIndex(r => r.HubId)
            .HasDatabaseName("idx_delivery_routes_hub_id");

        builder.HasIndex(r => r.ServiceDate)
            .HasDatabaseName("idx_delivery_routes_service_date");

        builder.HasIndex(r => r.OrderGroupId)
            .HasDatabaseName("idx_delivery_routes_order_group_id");

        builder.HasIndex(r => r.CreatedBy)
            .HasDatabaseName("idx_delivery_routes_created_by");
    }
}
