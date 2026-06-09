using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RestaurantStatusEnum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add new column with safe default so existing rows are immediately valid.
        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "restaurants",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "pending");

        // 2. Backfill: rows with is_approved = true become 'active'; the rest stay 'pending'.
        migrationBuilder.Sql(
            """
            UPDATE restaurants
            SET status = CASE WHEN "IsApproved" THEN 'active' ELSE 'pending' END;
            """);

        // 3. Drop the now-redundant boolean column.
        migrationBuilder.DropColumn(
            name: "IsApproved",
            table: "restaurants");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 1. Re-add the boolean column.
        migrationBuilder.AddColumn<bool>(
            name: "IsApproved",
            table: "restaurants",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // 2. Backfill: active → true, everything else → false.
        migrationBuilder.Sql(
            """
            UPDATE restaurants
            SET "IsApproved" = CASE WHEN status = 'active' THEN TRUE ELSE FALSE END;
            """);

        // 3. Drop the status column.
        migrationBuilder.DropColumn(
            name: "status",
            table: "restaurants");
    }
}
