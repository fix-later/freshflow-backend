using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProductCategoryHierarchy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ParentId",
            table: "product_categories",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_product_categories_ParentId",
            table: "product_categories",
            column: "ParentId");

        migrationBuilder.AddForeignKey(
            name: "FK_product_categories_product_categories_ParentId",
            table: "product_categories",
            column: "ParentId",
            principalTable: "product_categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_product_categories_product_categories_ParentId",
            table: "product_categories");

        migrationBuilder.DropIndex(
            name: "IX_product_categories_ParentId",
            table: "product_categories");

        migrationBuilder.DropColumn(
            name: "ParentId",
            table: "product_categories");
    }
}
