using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInvoicingModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InvoiceAddress",
            table: "restaurants",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InvoiceEmail",
            table: "restaurants",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InvoiceLegalName",
            table: "restaurants",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TaxCode",
            table: "restaurants",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VatRate",
            table: "products",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "invoices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                buyer_tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                buyer_legal_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                buyer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                buyer_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                serial = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                tax_authority_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                lookup_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                pdf_ref = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                xml_ref = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                provider_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                sub_total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                vat_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_invoices", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "invoice_lines",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                quantity = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                vat_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                vat_rate_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                line_subtotal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                line_vat_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                line_total = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_invoice_lines", x => x.id);
                table.ForeignKey(
                    name: "fk_invoice_lines_invoice",
                    column: x => x.invoice_id,
                    principalTable: "invoices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_invoice_lines_invoice_id",
            table: "invoice_lines",
            column: "invoice_id");

        migrationBuilder.CreateIndex(
            name: "idx_invoices_restaurant_id",
            table: "invoices",
            column: "restaurant_id");

        migrationBuilder.CreateIndex(
            name: "idx_invoices_status",
            table: "invoices",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "uq_invoices_order_id",
            table: "invoices",
            column: "order_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "invoice_lines");

        migrationBuilder.DropTable(
            name: "invoices");

        migrationBuilder.DropColumn(
            name: "InvoiceAddress",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "InvoiceEmail",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "InvoiceLegalName",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "TaxCode",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "VatRate",
            table: "products");
    }
}
