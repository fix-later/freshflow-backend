using FreshFlow.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.Persistence.Configurations;

internal sealed class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("driver_profiles");
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.UserId).IsUnique();
        builder.Property(d => d.UserId).IsRequired();
        builder.Property(d => d.LicensePlate).HasMaxLength(20);
        builder.Property(d => d.PhoneNumber).HasMaxLength(20);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();
    }
}
