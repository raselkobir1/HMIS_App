using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplementaryRunningBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SourceType_SourceId",
                schema: "billing",
                table: "Invoices");

            // Existing sourced invoices are part 1 of their source's bill.
            migrationBuilder.AddColumn<int>(
                name: "SourcePart",
                schema: "billing",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceType_SourceId_SourcePart",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "SourceType", "SourceId", "SourcePart" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_SourceType_SourceId_SourcePart",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SourcePart",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceType_SourceId",
                schema: "billing",
                table: "Invoices",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceId\" IS NOT NULL");
        }
    }
}
