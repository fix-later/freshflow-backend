using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHubs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hubs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                capacity_kg = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                occupied_capacity_kg = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                managed_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hubs", x => x.id);
                table.CheckConstraint("ck_hubs_capacity_kg_positive", "capacity_kg > 0");
            });

        migrationBuilder.CreateIndex(
            name: "idx_hubs_deleted_at",
            table: "hubs",
            column: "deleted_at");

        migrationBuilder.CreateIndex(
            name: "idx_hubs_is_active",
            table: "hubs",
            column: "is_active");

        migrationBuilder.CreateIndex(
            name: "idx_hubs_managed_by",
            table: "hubs",
            column: "managed_by");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hubs");
    }
}
