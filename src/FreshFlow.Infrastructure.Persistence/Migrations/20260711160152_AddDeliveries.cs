using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "deliveries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                sequence_number = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                estimated_arrival = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                actual_arrival = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                failure_reason = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deliveries", x => x.id);
                table.CheckConstraint("ck_deliveries_sequence_number", "sequence_number > 0");
                table.CheckConstraint("ck_deliveries_status", "status IN ('pending','arrived','delivered','failed')");
                table.ForeignKey(
                    name: "fk_deliveries_delivery_route",
                    column: x => x.delivery_route_id,
                    principalTable: "delivery_routes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_deliveries_delivery_route_id",
            table: "deliveries",
            column: "delivery_route_id");

        migrationBuilder.CreateIndex(
            name: "ux_deliveries_order_id",
            table: "deliveries",
            column: "order_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "deliveries");
    }
}
