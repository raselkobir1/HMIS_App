using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RunningBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargeKey",
                schema: "billing",
                table: "InvoiceLines",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId_ChargeKey",
                schema: "billing",
                table: "InvoiceLines",
                columns: new[] { "InvoiceId", "ChargeKey" },
                unique: true,
                filter: "\"ChargeKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_InvoiceId_ChargeKey",
                schema: "billing",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "ChargeKey",
                schema: "billing",
                table: "InvoiceLines");
        }
    }
}
