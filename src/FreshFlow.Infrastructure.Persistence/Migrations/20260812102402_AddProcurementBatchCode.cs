using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProcurementBatchCode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "code",
            table: "procurement_batches",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Code",
            table: "markets",
            type: "character varying(8)",
            maxLength: 8,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_procurement_batches_code",
            table: "procurement_batches",
            column: "code",
            unique: true,
            filter: "code IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_procurement_batches_code",
            table: "procurement_batches");

        migrationBuilder.DropColumn(
            name: "code",
            table: "procurement_batches");

        migrationBuilder.DropColumn(
            name: "Code",
            table: "markets");
    }
}
