using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCreditStatements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "credit_statements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                opening_balance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                closing_balance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                total_charges = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                total_settlements = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                total_refunds = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credit_statements", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "credit_statement_lines",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                credit_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                balance_after = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credit_statement_lines", x => x.id);
                table.ForeignKey(
                    name: "fk_credit_statement_lines_statement",
                    column: x => x.credit_statement_id,
                    principalTable: "credit_statements",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_credit_statement_lines_statement_id",
            table: "credit_statement_lines",
            column: "credit_statement_id");

        migrationBuilder.CreateIndex(
            name: "uq_credit_statements_restaurant_id_period_start",
            table: "credit_statements",
            columns: new[] { "restaurant_id", "period_start" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "credit_statement_lines");

        migrationBuilder.DropTable(
            name: "credit_statements");
    }
}
