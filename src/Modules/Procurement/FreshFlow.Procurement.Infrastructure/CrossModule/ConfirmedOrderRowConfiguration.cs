using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class ConfirmedOrderRowConfiguration : IEntityTypeConfiguration<ConfirmedOrderRow>
{
    public void Configure(EntityTypeBuilder<ConfirmedOrderRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT "Id", "Status", market_id, market_session_id, "ScheduledFor", deleted_at
            FROM orders
            WHERE deleted_at IS NULL
            """);
        builder.Property(row => row.Id).HasColumnName("Id");
        builder.Property(row => row.Status).HasColumnName("Status");
        builder.Property(row => row.MarketId).HasColumnName("market_id");
        builder.Property(row => row.MarketSessionId).HasColumnName("market_session_id");
        builder.Property(row => row.ScheduledFor).HasColumnName("ScheduledFor");
        builder.Property(row => row.DeletedAt).HasColumnName("deleted_at");
    }
}
