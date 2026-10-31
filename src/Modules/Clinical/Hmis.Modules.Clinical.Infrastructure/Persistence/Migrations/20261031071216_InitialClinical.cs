using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hmis.Modules.Clinical.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialClinical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clinical");

            migrationBuilder.CreateTable(
                name: "Consultations",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EncounterDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HistoryOfPresentIllness = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Examination = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Advice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FollowUpInstructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferralTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferralReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consultations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VitalSigns",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Systolic = table.Column<int>(type: "integer", nullable: true),
                    Diastolic = table.Column<int>(type: "integer", nullable: true),
                    Pulse = table.Column<int>(type: "integer", nullable: true),
                    TemperatureF = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    RespiratoryRate = table.Column<int>(type: "integer", nullable: true),
                    SpO2 = table.Column<int>(type: "integer", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
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
                    table.PrimaryKey("PK_VitalSigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationAddenda",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationAddenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationAddenda_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalSchema: "clinical",
                        principalTable: "Consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationDiagnoses",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationDiagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationDiagnoses_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalSchema: "clinical",
                        principalTable: "Consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationRequests",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestigationRequests_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalSchema: "clinical",
                        principalTable: "Consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                schema: "clinical",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Form = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Route = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DosePattern = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Timing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationValue = table.Column<int>(type: "integer", nullable: true),
                    DurationUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.CheckConstraint("CK_PrescriptionItems_Quantity", "\"Quantity\" IS NULL OR \"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalSchema: "clinical",
                        principalTable: "Consultations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationAddenda_ConsultationId",
                schema: "clinical",
                table: "ConsultationAddenda",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationDiagnoses_ConsultationId",
                schema: "clinical",
                table: "ConsultationDiagnoses",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_DoctorId_EncounterDate",
                schema: "clinical",
                table: "Consultations",
                columns: new[] { "DoctorId", "EncounterDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_EncounterType_EncounterId",
                schema: "clinical",
                table: "Consultations",
                columns: new[] { "EncounterType", "EncounterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Consultations_PatientId_EncounterDate",
                schema: "clinical",
                table: "Consultations",
                columns: new[] { "PatientId", "EncounterDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestigationRequests_ConsultationId",
                schema: "clinical",
                table: "InvestigationRequests",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_ConsultationId",
                schema: "clinical",
                table: "PrescriptionItems",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_VitalSigns_EncounterType_EncounterId",
                schema: "clinical",
                table: "VitalSigns",
                columns: new[] { "EncounterType", "EncounterId" });

            migrationBuilder.CreateIndex(
                name: "IX_VitalSigns_PatientId",
                schema: "clinical",
                table: "VitalSigns",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultationAddenda",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "ConsultationDiagnoses",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "InvestigationRequests",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "PrescriptionItems",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "VitalSigns",
                schema: "clinical");

            migrationBuilder.DropTable(
                name: "Consultations",
                schema: "clinical");
        }
    }
}
