using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

internal sealed class RestaurantTaxProfileRowConfiguration : IEntityTypeConfiguration<RestaurantTaxProfileRow>
{
    public void Configure(EntityTypeBuilder<RestaurantTaxProfileRow> builder)
    {
        // Read-only projection onto restaurants (Auth). ToSqlQuery avoids a model conflict with Auth's
        // ToTable("restaurants"). restaurants has no soft-delete column, so there is no deleted_at filter.
        // Columns are PascalCase (verified against Auth's RestaurantRowConfiguration).
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT "Id", "UserId", "Name", "TaxCode", "InvoiceLegalName", "InvoiceAddress", "InvoiceEmail"
            FROM restaurants
            """);

        builder.Property(r => r.Id);
        builder.Property(r => r.UserId);
        builder.Property(r => r.Name);
        builder.Property(r => r.TaxCode);
        builder.Property(r => r.InvoiceLegalName);
        builder.Property(r => r.InvoiceAddress);
        builder.Property(r => r.InvoiceEmail);
    }
}
