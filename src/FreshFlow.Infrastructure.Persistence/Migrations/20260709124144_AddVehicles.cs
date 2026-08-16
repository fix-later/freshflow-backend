using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddVehicles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "vehicles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                plate_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                capacity_kg = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                vehicle_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                is_available = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                registered_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vehicles", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_vehicles_deleted_at",
            table: "vehicles",
            column: "deleted_at");

        migrationBuilder.CreateIndex(
            name: "ux_vehicles_plate_number_active",
            table: "vehicles",
            column: "plate_number",
            unique: true,
            filter: "deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "vehicles");
    }
}
