using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProcurementExceptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "procurement_exceptions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                procurement_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                market_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                reported_quantity = table.Column<int>(type: "integer", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                proof_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_procurement_exceptions", x => x.id);
                table.ForeignKey(
                    name: "fk_procurement_exceptions_batch",
                    column: x => x.procurement_batch_id,
                    principalTable: "procurement_batches",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_procurement_exceptions_batch_id",
            table: "procurement_exceptions",
            column: "procurement_batch_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "procurement_exceptions");
    }
}
