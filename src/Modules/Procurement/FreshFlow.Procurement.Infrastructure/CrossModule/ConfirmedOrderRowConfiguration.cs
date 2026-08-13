using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class ConfirmedOrderRowConfiguration : IEntityTypeConfiguration<ConfirmedOrderRow>
{
    public void Configure(EntityTypeBuilder<ConfirmedOrderRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("orders");
        builder.Property(row => row.Id).HasColumnName("Id");
        builder.Property(row => row.Status).HasColumnName("Status");
        builder.Property(row => row.MarketId).HasColumnName("market_id");
        builder.Property(row => row.ScheduledFor).HasColumnName("ScheduledFor");
        builder.Property(row => row.DeletedAt).HasColumnName("deleted_at");
    }
}
