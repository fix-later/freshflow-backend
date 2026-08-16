using FreshFlow.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.CategoryId);
        builder.Property(p => p.UnitId).IsRequired();
        builder.Property(p => p.PackingCodeId);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.CreatedBy);

        // Legacy free-text fields kept for data transition (mapped to original column names)
        builder.Property(p => p.LegacyCategory).HasColumnName("category");
        builder.Property(p => p.LegacyUnit).HasColumnName("unit");

        builder.Property(p => p.ImageUrl).HasMaxLength(512);
        builder.Property(p => p.VatRate).HasMaxLength(10);
        builder.Property(p => p.MinimumOrderQuantity).IsRequired().HasDefaultValue(1);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.Property(p => p.DeletedAt);

        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.UnitOfMeasurement)
            .WithMany()
            .HasForeignKey(p => p.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.PackingCode)
            .WithMany()
            .HasForeignKey(p => p.PackingCodeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.PackingCodeId);
        builder.HasIndex(p => p.DeletedAt);
    }
}
