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

            -- Duplicate settlement references were previously allowed. Resolve any
            -- collisions (including ones created by the normalization above) so the
            -- unique index below cannot fail mid-migration: keep the earliest row per
            -- (restaurant_id, reference) and suffix later duplicates. left(...,158)
            -- keeps the result within reference's varchar(200) after the suffix.
            WITH ranked AS (
                SELECT id,
                       row_number() OVER (
                           PARTITION BY restaurant_id, reference
                           ORDER BY created_at, id
                       ) AS rn
                FROM credit_transactions
                WHERE type = 'settlement' AND reference IS NOT NULL
            )
            UPDATE credit_transactions ct
            SET reference = left(ct.reference, 158) || '-DUP-' || ct.id::text
            FROM ranked
            WHERE ct.id = ranked.id AND ranked.rn > 1;
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
