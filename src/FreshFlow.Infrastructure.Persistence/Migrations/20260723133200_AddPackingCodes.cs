using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPackingCodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PackingCodeId",
            table: "products",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "packing_codes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CapacityKg = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_packing_codes", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_products_PackingCodeId",
            table: "products",
            column: "PackingCodeId");

        migrationBuilder.CreateIndex(
            name: "IX_packing_codes_Code",
            table: "packing_codes",
            column: "Code",
            unique: true,
            filter: "\"DeletedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_packing_codes_DeletedAt",
            table: "packing_codes",
            column: "DeletedAt");

        migrationBuilder.AddForeignKey(
            name: "FK_products_packing_codes_PackingCodeId",
            table: "products",
            column: "PackingCodeId",
            principalTable: "packing_codes",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_products_packing_codes_PackingCodeId",
            table: "products");

        migrationBuilder.DropTable(
            name: "packing_codes");

        migrationBuilder.DropIndex(
            name: "IX_products_PackingCodeId",
            table: "products");

        migrationBuilder.DropColumn(
            name: "PackingCodeId",
            table: "products");
    }
}
