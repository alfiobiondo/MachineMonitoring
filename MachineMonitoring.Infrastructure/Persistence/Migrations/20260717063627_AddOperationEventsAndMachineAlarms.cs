using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationEventsAndMachineAlarms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sequence_number",
                table: "workpieces",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "machine_alarms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    machine_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_alarms", x => x.id);
                    table.ForeignKey(
                        name: "FK_machine_alarms_machine_operations_machine_operation_id",
                        column: x => x.machine_operation_id,
                        principalTable: "machine_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "machine_operation_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    new_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    progress_percentage = table.Column<int>(type: "integer", nullable: true),
                    phase = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    machine_alarm_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_operation_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_machine_operation_events_machine_alarms_machine_alarm_id",
                        column: x => x.machine_alarm_id,
                        principalTable: "machine_alarms",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_machine_operation_events_machine_operations_machine_operati~",
                        column: x => x.machine_operation_id,
                        principalTable: "machine_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                WITH numbered_workpieces AS (
                    SELECT
                        id,
                        ROW_NUMBER() OVER (
                            PARTITION BY production_lot_id
                            ORDER BY created_at, id
                        ) AS sequence_number
                    FROM workpieces
                )
                UPDATE workpieces AS target
                SET sequence_number = numbered_workpieces.sequence_number
                FROM numbered_workpieces
                WHERE target.id = numbered_workpieces.id;
                """
            );

            migrationBuilder.AlterColumn<int>(
                name: "sequence_number",
                table: "workpieces",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workpieces_production_lot_id_sequence_number",
                table: "workpieces",
                columns: new[] { "production_lot_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workpieces_production_lot_id_status_sequence_number",
                table: "workpieces",
                columns: new[] { "production_lot_id", "status", "sequence_number" });

            migrationBuilder.CreateIndex(
                name: "IX_machine_alarms_machine_id",
                table: "machine_alarms",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_machine_alarms_machine_id_status",
                table: "machine_alarms",
                columns: new[] { "machine_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_machine_alarms_machine_operation_id",
                table: "machine_alarms",
                column: "machine_operation_id");

            migrationBuilder.CreateIndex(
                name: "IX_machine_alarms_status",
                table: "machine_alarms",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_machine_operation_events_machine_alarm_id",
                table: "machine_operation_events",
                column: "machine_alarm_id");

            migrationBuilder.CreateIndex(
                name: "IX_machine_operation_events_machine_operation_id_occurred_at",
                table: "machine_operation_events",
                columns: new[] { "machine_operation_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_operation_events");

            migrationBuilder.DropTable(
                name: "machine_alarms");

            migrationBuilder.DropIndex(
                name: "IX_workpieces_production_lot_id_sequence_number",
                table: "workpieces");

            migrationBuilder.DropIndex(
                name: "IX_workpieces_production_lot_id_status_sequence_number",
                table: "workpieces");

            migrationBuilder.DropColumn(
                name: "sequence_number",
                table: "workpieces");
        }
    }
}
