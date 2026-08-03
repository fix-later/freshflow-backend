using FreshFlow.Invoicing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Invoicing.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(l => l.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
        builder.Property(l => l.Unit).HasColumnName("unit").HasMaxLength(100);
        builder.Property(l => l.Quantity).HasColumnName("quantity").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(l => l.VatRateCode).HasColumnName("vat_rate_code").HasMaxLength(10).IsRequired();
        builder.Property(l => l.VatRatePercent).HasColumnName("vat_rate_percent").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(l => l.LineSubtotal).HasColumnName("line_subtotal").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(l => l.LineVatAmount).HasColumnName("line_vat_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(l => l.LineTotal).HasColumnName("line_total").HasColumnType("numeric(14,2)").IsRequired();

        builder.HasIndex(l => l.InvoiceId).HasDatabaseName("idx_invoice_lines_invoice_id");
    }
}
