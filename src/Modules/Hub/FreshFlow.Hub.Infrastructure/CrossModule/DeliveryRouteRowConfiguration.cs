using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class DeliveryRouteRowConfiguration : IEntityTypeConfiguration<DeliveryRouteRow>
{
    public void Configure(EntityTypeBuilder<DeliveryRouteRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """SELECT id AS "RouteId", status AS "Status" FROM delivery_routes WHERE deleted_at IS NULL""");
        builder.Property(r => r.RouteId);
        builder.Property(r => r.Status);
    }
}
