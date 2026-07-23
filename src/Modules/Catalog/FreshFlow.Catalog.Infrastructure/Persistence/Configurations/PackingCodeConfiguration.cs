using FreshFlow.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class PackingCodeConfiguration : IEntityTypeConfiguration<PackingCode>
{
    public void Configure(EntityTypeBuilder<PackingCode> builder)
    {
        builder.ToTable("packing_codes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CapacityKg).IsRequired().HasColumnType("numeric(12,3)");
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.DeletedAt);

        builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(x => x.DeletedAt);
    }
}
