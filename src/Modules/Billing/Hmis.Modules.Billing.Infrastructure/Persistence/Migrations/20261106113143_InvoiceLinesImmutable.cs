using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceLinesImmutable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lines of a finalized or cancelled invoice are part of the financial record (BRD Module 17 rule 8): only a
            // draft's lines may be changed or removed. A missing parent means the invoice row itself is being deleted
            // (cascade), which billing.prevent_financial_change on Invoices already governs, so it is not blocked here.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION billing.prevent_invoice_line_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    invoice_status text;
                BEGIN
                    SELECT "Status" INTO invoice_status FROM billing."Invoices" WHERE "Id" = OLD."InvoiceId";
                    IF invoice_status IS NOT NULL AND invoice_status <> 'Draft' THEN
                        RAISE EXCEPTION 'billing.InvoiceLines of a % invoice are a financial record: % is not allowed', invoice_status, TG_OP
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RETURN OLD;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER "TR_InvoiceLines_DraftOnly" BEFORE UPDATE OR DELETE ON billing."InvoiceLines"
                    FOR EACH ROW EXECUTE FUNCTION billing.prevent_invoice_line_change();
                CREATE TRIGGER "TR_InvoiceLines_NoTruncate" BEFORE TRUNCATE ON billing."InvoiceLines"
                    FOR EACH STATEMENT EXECUTE FUNCTION billing.prevent_financial_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_InvoiceLines_DraftOnly" ON billing."InvoiceLines";
                DROP TRIGGER IF EXISTS "TR_InvoiceLines_NoTruncate" ON billing."InvoiceLines";
                DROP FUNCTION IF EXISTS billing.prevent_invoice_line_change();
                """);
        }
    }
}
