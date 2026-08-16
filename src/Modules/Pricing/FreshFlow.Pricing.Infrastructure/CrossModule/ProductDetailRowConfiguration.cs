using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

internal sealed class ProductDetailRowConfiguration : IEntityTypeConfiguration<ProductDetailRow>
{
    public void Configure(EntityTypeBuilder<ProductDetailRow> builder)
    {
        // Read-only projection joining products (Catalog) with units_of_measurement and
        // product_categories. Products are filtered to non-deleted rows only.
        // ToSqlQuery avoids a model conflict with Catalog's ToTable("products").
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                p."Id",
                p."Name",
                p."ImageUrl",
                u."Name"  AS "Unit",
                c."Name"  AS "Category",
                pc."CapacityKg"
            FROM products p
            INNER JOIN units_of_measurement u ON p."UnitId" = u."Id"
            LEFT  JOIN product_categories   c ON p."CategoryId" = c."Id"
            LEFT  JOIN packing_codes       pc ON p."PackingCodeId" = pc."Id" AND pc."DeletedAt" IS NULL
            WHERE p."DeletedAt" IS NULL
            """);

        builder.Property(p => p.Id);
        builder.Property(p => p.Name);
        builder.Property(p => p.ImageUrl);
        builder.Property(p => p.Unit);
        builder.Property(p => p.Category);
        builder.Property(p => p.CapacityKg);
    }
}
