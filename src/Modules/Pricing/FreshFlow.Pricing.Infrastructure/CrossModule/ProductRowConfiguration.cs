using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

internal sealed class ProductRowConfiguration : IEntityTypeConfiguration<ProductRow>
{
    public void Configure(EntityTypeBuilder<ProductRow> builder)
    {
        // Read-only projection onto the products table owned by Catalog.Product.
        // ToSqlQuery avoids a model conflict with Catalog's ToTable("products").
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id", "Name", "IsActive" FROM products WHERE "DeletedAt" IS NULL""");
        builder.Property(p => p.Id);
        builder.Property(p => p.Name);
        builder.Property(p => p.IsActive);
    }
}
