using FreshFlow.Invoicing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Invoicing.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(i => i.RestaurantId).HasColumnName("restaurant_id").IsRequired();

        builder.Property(i => i.BuyerTaxCode).HasColumnName("buyer_tax_code").HasMaxLength(20).IsRequired();
        builder.Property(i => i.BuyerLegalName).HasColumnName("buyer_legal_name").HasMaxLength(300).IsRequired();
        builder.Property(i => i.BuyerAddress).HasColumnName("buyer_address").HasMaxLength(500);
        builder.Property(i => i.BuyerEmail).HasColumnName("buyer_email").HasMaxLength(256);

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Serial).HasColumnName("serial").HasMaxLength(20);
        builder.Property(i => i.Number).HasColumnName("number").HasMaxLength(30);
        builder.Property(i => i.TaxAuthorityCode).HasColumnName("tax_authority_code").HasMaxLength(50);
        builder.Property(i => i.LookupUrl).HasColumnName("lookup_url").HasMaxLength(512);
        builder.Property(i => i.PdfRef).HasColumnName("pdf_ref").HasMaxLength(512);
        builder.Property(i => i.XmlRef).HasColumnName("xml_ref").HasMaxLength(512);
        builder.Property(i => i.ProviderName).HasColumnName("provider_name").HasMaxLength(50);
        builder.Property(i => i.IssuedAt).HasColumnName("issued_at");
        builder.Property(i => i.ErrorReason).HasColumnName("error_reason").HasMaxLength(1000);
        builder.Property(i => i.RetryCount).HasColumnName("retry_count").IsRequired();

        builder.Property(i => i.SubTotal).HasColumnName("sub_total").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(i => i.VatAmount).HasColumnName("vat_amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(i => i.Total).HasColumnName("total").HasColumnType("numeric(14,2)").IsRequired();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");

        // One invoice per order (v1) — DB-level idempotency against duplicate DeliveryCompleted events.
        builder.HasIndex(i => i.OrderId).IsUnique().HasDatabaseName("uq_invoices_order_id");
        builder.HasIndex(i => i.RestaurantId).HasDatabaseName("idx_invoices_restaurant_id");
        builder.HasIndex(i => i.Status).HasDatabaseName("idx_invoices_status");

        builder.HasMany(i => i.Lines)
            .WithOne()
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_invoice_lines_invoice");

        builder.Metadata.FindNavigation(nameof(Invoice.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
