using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRolesTable : Migration
{
    // Deterministic IDs for the 6 seed roles — keep in sync with AdminSeeder.SeedRoles
    private static readonly Guid AdminRoleId = new("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid MarketAgentRoleId = new("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid RestaurantRoleId = new("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid HubStaffRoleId = new("a0000000-0000-0000-0000-000000000004");
    private static readonly Guid DriverRoleId = new("a0000000-0000-0000-0000-000000000005");
    private static readonly Guid OpsManagerRoleId = new("a0000000-0000-0000-0000-000000000006");

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Create roles table
        migrationBuilder.CreateTable(
            name: "roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_roles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_roles_Name",
            table: "roles",
            column: "Name",
            unique: true);

        // 2. Seed the 6 canonical roles with deterministic IDs
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        migrationBuilder.InsertData(
            table: "roles",
            columns: ["Id", "Name", "Description", "CreatedAt"],
            values: new object[,]
            {
                { AdminRoleId,       "admin",              "System administrator with full access",                   now },
                { MarketAgentRoleId, "market_agent",       "Market agent or kiosk staff managing a market",          now },
                { RestaurantRoleId,  "restaurant",         "Restaurant owner or operator",                           now },
                { HubStaffRoleId,    "hub_staff",          "Hub warehouse and logistics staff",                      now },
                { DriverRoleId,      "driver",             "Delivery driver",                                        now },
                { OpsManagerRoleId,  "operations_manager", "Operations manager with cross-module visibility",        now }
            });

        // 3. Add RoleId column as NULLABLE first so existing rows don't violate NOT NULL
        migrationBuilder.AddColumn<Guid>(
            name: "RoleId",
            table: "users",
            type: "uuid",
            nullable: true);

        // 4. Populate RoleId from old Role (text) column.
        // No ELSE branch: unrecognised role strings stay NULL and will trip the
        // NOT NULL constraint in step 5, surfacing data issues instead of silently
        // granting admin access.
        migrationBuilder.Sql($"""
            UPDATE users SET "RoleId" = CASE "Role"
                WHEN 'admin'              THEN '{AdminRoleId}'::uuid
                WHEN 'market_agent'       THEN '{MarketAgentRoleId}'::uuid
                WHEN 'hub_staff'          THEN '{HubStaffRoleId}'::uuid
                WHEN 'driver'             THEN '{DriverRoleId}'::uuid
                WHEN 'restaurant'         THEN '{RestaurantRoleId}'::uuid
                WHEN 'operations_manager' THEN '{OpsManagerRoleId}'::uuid
            END
            WHERE "RoleId" IS NULL;
            """);

        // 5. Make RoleId NOT NULL now that all rows have a value
        migrationBuilder.AlterColumn<Guid>(
            name: "RoleId",
            table: "users",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        // 6. Add optional phone login identifier.
        migrationBuilder.AddColumn<string>(
            name: "Phone",
            table: "users",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_Phone",
            table: "users",
            column: "Phone",
            unique: true,
            filter: "\"Phone\" IS NOT NULL");

        // 7. Drop the old text-based Role column
        migrationBuilder.DropColumn(
            name: "Role",
            table: "users");

        // 8. Create index and FK
        migrationBuilder.CreateIndex(
            name: "IX_users_RoleId",
            table: "users",
            column: "RoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_users_roles_RoleId",
            table: "users",
            column: "RoleId",
            principalTable: "roles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_users_roles_RoleId",
            table: "users");

        migrationBuilder.DropIndex(
            name: "IX_users_RoleId",
            table: "users");

        migrationBuilder.DropIndex(
            name: "IX_users_Phone",
            table: "users");

        migrationBuilder.DropColumn(
            name: "Phone",
            table: "users");

        // Re-add the text Role column and restore values from the roles table
        migrationBuilder.AddColumn<string>(
            name: "Role",
            table: "users",
            type: "text",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE users u
            SET "Role" = r."Name"
            FROM roles r
            WHERE u."RoleId" = r."Id";
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Role",
            table: "users",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "RoleId",
            table: "users");

        migrationBuilder.DropTable(
            name: "roles");
    }
}
