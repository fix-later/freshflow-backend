using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderReceiptIssues : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "confirmed_receipt_at",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "order_issues",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                issue_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                affected_quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_issues", x => x.id);
                table.ForeignKey(
                    name: "fk_order_issues_order",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_order_issues_order_item",
                    column: x => x.order_item_id,
                    principalTable: "order_items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "idx_order_issues_deleted_at",
            table: "order_issues",
            column: "deleted_at");

        migrationBuilder.CreateIndex(
            name: "idx_order_issues_order_id",
            table: "order_issues",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_order_issues_order_item_id",
            table: "order_issues",
            column: "order_item_id");

        migrationBuilder.CreateIndex(
            name: "idx_order_issues_status",
            table: "order_issues",
            column: "status");

        // Cross-module FK: reported_by references Auth's users table by ID only.
        // Orders cannot declare a model-level navigation to Auth.User without violating
        // module dependency boundaries, so keep this database constraint as raw SQL.
        migrationBuilder.Sql(
            """
            ALTER TABLE order_issues ADD CONSTRAINT fk_order_issues_reported_by
                FOREIGN KEY (reported_by) REFERENCES users ("Id") ON DELETE RESTRICT;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE order_issues DROP CONSTRAINT fk_order_issues_reported_by;
            """);

        migrationBuilder.DropTable(
            name: "order_issues");

        migrationBuilder.DropColumn(
            name: "confirmed_receipt_at",
            table: "orders");
    }
}
