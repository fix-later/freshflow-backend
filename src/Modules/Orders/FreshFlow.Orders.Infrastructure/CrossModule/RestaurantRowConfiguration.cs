using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class RestaurantRowConfiguration : IEntityTypeConfiguration<RestaurantRow>
{
    public void Configure(EntityTypeBuilder<RestaurantRow> builder)
    {
        // Read-only projection onto restaurants (Auth module).
        // ToSqlQuery avoids a model conflict with Auth's ToTable("restaurants").
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT "Id", "UserId", status AS "Status" FROM restaurants""");
        builder.Property(r => r.Id);
        builder.Property(r => r.UserId);
        builder.Property(r => r.Status);
    }
}
