using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineRuntimeStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machine_runtime_states",
                columns: table => new
                {
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    active_alarm_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_runtime_states", x => x.machine_id);
                    table.ForeignKey(
                        name: "FK_machine_runtime_states_machine_alarms_active_alarm_id",
                        column: x => x.active_alarm_id,
                        principalTable: "machine_alarms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_machine_runtime_states_machine_operations_current_operation~",
                        column: x => x.current_operation_id,
                        principalTable: "machine_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_machine_runtime_states_active_alarm_id",
                table: "machine_runtime_states",
                column: "active_alarm_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_machine_runtime_states_current_operation_id",
                table: "machine_runtime_states",
                column: "current_operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_machine_runtime_states_status",
                table: "machine_runtime_states",
                column: "status");

            migrationBuilder.Sql(
                """
                INSERT INTO machine_runtime_states (
                    machine_id,
                    status,
                    current_operation_id,
                    last_changed_at,
                    failure_reason,
                    active_alarm_id,
                    version
                )
                SELECT
                    mc.machine_id,
                    'Available',
                    NULL,
                    CURRENT_TIMESTAMP,
                    NULL,
                    NULL,
                    1
                FROM machine_capabilities AS mc
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM machine_runtime_states AS mrs
                    WHERE mrs.machine_id = mc.machine_id
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_runtime_states");
        }
    }
}
