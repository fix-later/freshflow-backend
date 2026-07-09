using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class DriverRowConfiguration : IEntityTypeConfiguration<DriverRow>
{
    public void Configure(EntityTypeBuilder<DriverRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT u."Id" AS "UserId", r."Name" AS "RoleName", u."IsActive" AS "IsActive"
            FROM users u
            JOIN roles r ON r."Id" = u."RoleId"
            WHERE u."DeletedAt" IS NULL
            """);
        builder.Property(d => d.UserId);
        builder.Property(d => d.RoleName);
        builder.Property(d => d.IsActive);
    }
}
