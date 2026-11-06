using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Appointment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationInProgressIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_InConsultation",
                schema: "appointment",
                table: "Appointments",
                column: "DoctorId",
                unique: true,
                filter: "\"Status\" = 'InConsultation'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_InConsultation",
                schema: "appointment",
                table: "Appointments");
        }
    }
}
