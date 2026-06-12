using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

internal sealed class UserMarketAssignmentRowConfiguration
    : IEntityTypeConfiguration<UserMarketAssignmentRow>
{
    public void Configure(EntityTypeBuilder<UserMarketAssignmentRow> builder)
    {
        // Read-only projection onto the user_market_assignments table owned by Auth.
        // ToSqlQuery avoids a model conflict with Auth's entity mapping.
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "UserId", "MarketId" FROM user_market_assignments""");
        builder.Property(u => u.UserId);
        builder.Property(u => u.MarketId);
    }
}
