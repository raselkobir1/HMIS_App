using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Nursing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNursing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "nursing");

            migrationBuilder.CreateTable(
                name: "IntakeOutputEntries",
                schema: "nursing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VolumeMl = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsEnteredInError = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeOutputEntries", x => x.Id);
                    table.CheckConstraint("CK_IntakeOutputEntries_Volume", "\"VolumeMl\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "MedicationAdministrations",
                schema: "nursing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoseGiven = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnteredInError = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationAdministrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NurseAssignments",
                schema: "nursing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsInCharge = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NurseAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NursingNotes",
                schema: "nursing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsEnteredInError = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorReason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftHandovers",
                schema: "nursing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Shift = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftHandovers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HandoverItems",
                schema: "nursing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HandoverId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverItems_ShiftHandovers_HandoverId",
                        column: x => x.HandoverId,
                        principalSchema: "nursing",
                        principalTable: "ShiftHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverItems_HandoverId",
                schema: "nursing",
                table: "HandoverItems",
                column: "HandoverId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeOutputEntries_AdmissionId_MeasuredAt",
                schema: "nursing",
                table: "IntakeOutputEntries",
                columns: new[] { "AdmissionId", "MeasuredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationAdministrations_AdmissionId_OccurredAt",
                schema: "nursing",
                table: "MedicationAdministrations",
                columns: new[] { "AdmissionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationAdministrations_AdmissionId_ScheduledAt",
                schema: "nursing",
                table: "MedicationAdministrations",
                columns: new[] { "AdmissionId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicationAdministrations_MedicationOrderId_ScheduledAt",
                schema: "nursing",
                table: "MedicationAdministrations",
                columns: new[] { "MedicationOrderId", "ScheduledAt" },
                unique: true,
                filter: "\"ScheduledAt\" IS NOT NULL AND NOT \"IsEnteredInError\"");

            migrationBuilder.CreateIndex(
                name: "IX_NurseAssignments_EmployeeId_Date",
                schema: "nursing",
                table: "NurseAssignments",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_NurseAssignments_InCharge",
                schema: "nursing",
                table: "NurseAssignments",
                columns: new[] { "WardId", "Date", "Shift" },
                unique: true,
                filter: "\"IsInCharge\"");

            migrationBuilder.CreateIndex(
                name: "IX_NurseAssignments_WardId_Date_Shift_EmployeeId",
                schema: "nursing",
                table: "NurseAssignments",
                columns: new[] { "WardId", "Date", "Shift", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NursingNotes_AdmissionId_CreatedAt",
                schema: "nursing",
                table: "NursingNotes",
                columns: new[] { "AdmissionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftHandovers_WardId_Date_Shift",
                schema: "nursing",
                table: "ShiftHandovers",
                columns: new[] { "WardId", "Date", "Shift" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoverItems",
                schema: "nursing");

            migrationBuilder.DropTable(
                name: "IntakeOutputEntries",
                schema: "nursing");

            migrationBuilder.DropTable(
                name: "MedicationAdministrations",
                schema: "nursing");

            migrationBuilder.DropTable(
                name: "NurseAssignments",
                schema: "nursing");

            migrationBuilder.DropTable(
                name: "NursingNotes",
                schema: "nursing");

            migrationBuilder.DropTable(
                name: "ShiftHandovers",
                schema: "nursing");
        }
    }
}
