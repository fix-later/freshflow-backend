using FreshFlow.Logistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(v => v.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.CapacityKg)
            .HasColumnName("capacity_kg")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(v => v.VehicleType)
            .HasColumnName("vehicle_type")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(v => v.IsAvailable)
            .HasColumnName("is_available")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(v => v.RegisteredBy)
            .HasColumnName("registered_by");

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(v => v.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(v => v.PlateNumber)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_vehicles_plate_number_active");

        builder.HasIndex(v => v.DeletedAt)
            .HasDatabaseName("idx_vehicles_deleted_at");
    }
}
