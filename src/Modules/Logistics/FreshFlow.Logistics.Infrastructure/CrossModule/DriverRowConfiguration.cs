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
            SELECT u."Id" AS "UserId", u."FullName", u."Email", r."Name" AS "RoleName",
                   u."IsActive" AS "IsActive", hda.hub_id AS "HubId"
            FROM users u
            JOIN roles r ON r."Id" = u."RoleId"
            LEFT JOIN hub_driver_assignments hda ON hda.user_id = u."Id"
            WHERE u."DeletedAt" IS NULL
            """);
        builder.Property(d => d.UserId);
        builder.Property(d => d.FullName);
        builder.Property(d => d.Email);
        builder.Property(d => d.RoleName);
        builder.Property(d => d.IsActive);
        builder.Property(d => d.HubId);
    }
}
