using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class UserMarketAssignmentRowConfiguration
    : IEntityTypeConfiguration<UserMarketAssignmentRow>
{
    public void Configure(EntityTypeBuilder<UserMarketAssignmentRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "UserId", "MarketId" FROM user_market_assignments""");
        builder.Property(row => row.UserId);
        builder.Property(row => row.MarketId);
    }
}
