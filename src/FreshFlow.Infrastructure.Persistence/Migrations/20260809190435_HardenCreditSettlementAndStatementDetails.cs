using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HardenCreditSettlementAndStatementDetails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "recorded_by_user_id",
            table: "credit_transactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "order_id",
            table: "credit_statement_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "payment_method",
            table: "credit_statement_lines",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE credit_transactions
            SET reference = 'LEGACY-' || id::text
            WHERE type = 'settlement'
              AND (reference IS NULL OR btrim(reference) = '');

            UPDATE credit_transactions
            SET reference = upper(btrim(reference))
            WHERE type = 'settlement';
            """);

        migrationBuilder.CreateIndex(
            name: "uq_credit_transactions_settlement_reference",
            table: "credit_transactions",
            columns: new[] { "restaurant_id", "reference" },
            unique: true,
            filter: "type = 'settlement' AND reference IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_credit_transactions_settlement_reference",
            table: "credit_transactions",
            sql: "type <> 'settlement' OR (reference IS NOT NULL AND btrim(reference) <> '')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "uq_credit_transactions_settlement_reference",
            table: "credit_transactions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_credit_transactions_settlement_reference",
            table: "credit_transactions");

        migrationBuilder.DropColumn(
            name: "recorded_by_user_id",
            table: "credit_transactions");

        migrationBuilder.DropColumn(
            name: "order_id",
            table: "credit_statement_lines");

        migrationBuilder.DropColumn(
            name: "payment_method",
            table: "credit_statement_lines");
    }
}
