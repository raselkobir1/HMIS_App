using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.HR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoctorInpatientVisitFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InpatientVisitFee",
                schema: "hr",
                table: "DoctorProfiles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InpatientVisitFee",
                schema: "hr",
                table: "DoctorProfiles");
        }
    }
}
