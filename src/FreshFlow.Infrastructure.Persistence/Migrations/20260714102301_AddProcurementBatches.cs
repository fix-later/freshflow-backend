using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProcurementBatches : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "procurement_batches",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                batch_date = table.Column<DateOnly>(type: "date", nullable: false),
                market_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Built"),
                total_item_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_procurement_batches", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "procurement_batch_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                procurement_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                market_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                total_quantity = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_procurement_batch_items", x => x.id);
                table.ForeignKey(
                    name: "fk_procurement_batch_items_batch",
                    column: x => x.procurement_batch_id,
                    principalTable: "procurement_batches",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "procurement_batch_orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                procurement_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_procurement_batch_orders", x => x.id);
                table.ForeignKey(
                    name: "fk_procurement_batch_orders_batch",
                    column: x => x.procurement_batch_id,
                    principalTable: "procurement_batches",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_procurement_batch_items_batch_id",
            table: "procurement_batch_items",
            column: "procurement_batch_id");

        migrationBuilder.CreateIndex(
            name: "idx_procurement_batch_orders_batch_id",
            table: "procurement_batch_orders",
            column: "procurement_batch_id");

        migrationBuilder.CreateIndex(
            name: "ux_procurement_batch_orders_order_active",
            table: "procurement_batch_orders",
            column: "order_id",
            unique: true,
            filter: "\"deleted_at\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "idx_procurement_batches_batch_date_market_id",
            table: "procurement_batches",
            columns: new[] { "batch_date", "market_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "procurement_batch_items");

        migrationBuilder.DropTable(
            name: "procurement_batch_orders");

        migrationBuilder.DropTable(
            name: "procurement_batches");
    }
}
