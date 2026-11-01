using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Clinical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InpatientCare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicationOrders",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderedByDoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Form = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Route = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Dose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    PrnIndication = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StoppedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StoppedByDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                    StopReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationOrders", x => x.Id);
                    table.CheckConstraint("CK_MedicationOrders_Duration", "\"DurationDays\" IS NULL OR \"DurationDays\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ProgressNotes",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Subjective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Objective = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Assessment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Plan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsEnteredInError = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationOrders_AdmissionId",
                schema: "clinical",
                table: "MedicationOrders",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicationOrders_PatientId",
                schema: "clinical",
                table: "MedicationOrders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressNotes_AdmissionId_CreatedAt",
                schema: "clinical",
                table: "ProgressNotes",
                columns: new[] { "AdmissionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressNotes_PatientId",
                schema: "clinical",
                table: "ProgressNotes",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicationOrders",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "ProgressNotes",
                schema: "clinical");
        }
    }
}
