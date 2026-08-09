using System.Text.Json;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.Persistence.Configurations;

internal sealed class RoutePlanConfiguration : IEntityTypeConfiguration<RoutePlan>
{
    public void Configure(EntityTypeBuilder<RoutePlan> builder)
    {
        builder.ToTable("route_plans", table => table.HasCheckConstraint(
            "ck_route_plans_status", "status IN ('proposed','approved','stale','superseded')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HubId).HasColumnName("hub_id");
        builder.Property(x => x.ServiceDate).HasColumnName("service_date").HasColumnType("date");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.OptimizationCriteria).HasColumnName("optimization_criteria").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RoutingProvider).HasColumnName("routing_provider").HasMaxLength(40);
        builder.Property(x => x.IsEstimated).HasColumnName("is_estimated");
        builder.Property(x => x.InputRevision).HasColumnName("input_revision").HasMaxLength(64);
        builder.Property(x => x.VehiclesUsed).HasColumnName("vehicles_used");
        builder.Property(x => x.TotalLoadKg).HasColumnName("total_load_kg").HasColumnType("numeric(14,3)");
        builder.Property(x => x.TotalDistanceKm).HasColumnName("total_distance_km").HasColumnType("numeric(14,2)");
        builder.Property(x => x.EstimatedDurationMinutes).HasColumnName("estimated_duration_minutes");
        builder.Property(x => x.EstimatedCost).HasColumnName("estimated_cost").HasColumnType("numeric(16,2)");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        var comparer = new ValueComparer<IReadOnlyList<RoutePlanUnassigned>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
            value => value.ToList().AsReadOnly());
        builder.Property(x => x.Unassigned)
            .HasColumnName("unassigned_json").HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<List<RoutePlanUnassigned>>(value, (JsonSerializerOptions?)null)
                    ?? new List<RoutePlanUnassigned>())
            .Metadata.SetValueComparer(comparer);

        builder.HasIndex(x => new { x.HubId, x.ServiceDate }, "ux_route_plans_hub_date_proposed")
            .IsUnique().HasFilter("status = 'proposed' AND deleted_at IS NULL")
            .HasDatabaseName("ux_route_plans_hub_date_proposed");
        builder.HasIndex(x => x.InputRevision).HasDatabaseName("idx_route_plans_input_revision");
    }
}
