using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubConfiguration : IEntityTypeConfiguration<HubEntity>
{
    public void Configure(EntityTypeBuilder<HubEntity> builder)
    {
        builder.ToTable("hubs", table =>
        {
            table.HasCheckConstraint("ck_hubs_capacity_kg_positive", "capacity_kg > 0");
        });

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(h => h.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(h => h.Address)
            .HasColumnName("address")
            .HasMaxLength(500);

        builder.Property(h => h.Latitude)
            .HasColumnName("latitude")
            .HasColumnType("numeric(9,6)");

        builder.Property(h => h.Longitude)
            .HasColumnName("longitude")
            .HasColumnType("numeric(9,6)");

        builder.Property(h => h.CapacityKg)
            .HasColumnName("capacity_kg")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(h => h.OccupiedCapacityKg)
            .HasColumnName("occupied_capacity_kg")
            .HasColumnType("numeric(10,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Ignore(h => h.AvailableCapacityKg);

        builder.Property(h => h.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(h => h.ManagedBy)
            .HasColumnName("managed_by");

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(h => h.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(h => h.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(h => h.IsActive)
            .HasDatabaseName("idx_hubs_is_active");

        builder.HasIndex(h => h.ManagedBy)
            .HasDatabaseName("idx_hubs_managed_by");

        builder.HasIndex(h => h.DeletedAt)
            .HasDatabaseName("idx_hubs_deleted_at");
    }
}
