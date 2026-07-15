using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machine_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workpiece_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    progress_percentage = table.Column<int>(type: "integer", nullable: false),
                    current_phase = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_operations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "laser_cut_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nozzle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    drawing_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometry_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    thickness_millimeters = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    tube_outer_diameter_millimeters = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    tube_length_millimeters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    sheet_width_millimeters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    sheet_height_millimeters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    laser_power_watts = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    cutting_speed_millimeters_per_minute = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    assist_gas = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gas_pressure_bar = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    focal_offset_millimeters = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    number_of_passes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laser_cut_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_laser_cut_configurations_drawing_files_drawing_file_id",
                        column: x => x.drawing_file_id,
                        principalTable: "drawing_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laser_cut_configurations_machine_operations_operation_id",
                        column: x => x.operation_id,
                        principalTable: "machine_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_laser_cut_configurations_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_laser_cut_configurations_nozzles_nozzle_id",
                        column: x => x.nozzle_id,
                        principalTable: "nozzles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_laser_cut_configurations_drawing_file_id",
                table: "laser_cut_configurations",
                column: "drawing_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_laser_cut_configurations_material_id",
                table: "laser_cut_configurations",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_laser_cut_configurations_nozzle_id",
                table: "laser_cut_configurations",
                column: "nozzle_id");

            migrationBuilder.CreateIndex(
                name: "IX_laser_cut_configurations_operation_id",
                table: "laser_cut_configurations",
                column: "operation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_machine_operations_machine_id",
                table: "machine_operations",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "IX_machine_operations_status",
                table: "machine_operations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "laser_cut_configurations");

            migrationBuilder.DropTable(
                name: "machine_operations");
        }
    }
}
