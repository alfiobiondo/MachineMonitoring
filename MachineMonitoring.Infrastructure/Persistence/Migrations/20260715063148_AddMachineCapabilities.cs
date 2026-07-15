using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machine_capabilities",
                columns: table => new
                {
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    maximum_laser_power_watts = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    minimum_thickness_millimeters = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    maximum_thickness_millimeters = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    maximum_tube_diameter_millimeters = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    maximum_tube_length_millimeters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    maximum_sheet_width_millimeters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    maximum_sheet_height_millimeters = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_capabilities", x => x.machine_id);
                });

            migrationBuilder.CreateTable(
                name: "machine_capability_geometry_types",
                columns: table => new
                {
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    geometry_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_capability_geometry_types", x => new { x.machine_id, x.geometry_type });
                    table.ForeignKey(
                        name: "FK_machine_capability_geometry_types_machine_capabilities_mach~",
                        column: x => x.machine_id,
                        principalTable: "machine_capabilities",
                        principalColumn: "machine_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_capability_material_categories",
                columns: table => new
                {
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    material_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_capability_material_categories", x => new { x.machine_id, x.material_category });
                    table.ForeignKey(
                        name: "FK_machine_capability_material_categories_machine_capabilities~",
                        column: x => x.machine_id,
                        principalTable: "machine_capabilities",
                        principalColumn: "machine_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "machine_capability_nozzles",
                columns: table => new
                {
                    machine_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nozzle_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_capability_nozzles", x => new { x.machine_id, x.nozzle_id });
                    table.ForeignKey(
                        name: "FK_machine_capability_nozzles_machine_capabilities_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machine_capabilities",
                        principalColumn: "machine_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_machine_capability_nozzles_nozzles_nozzle_id",
                        column: x => x.nozzle_id,
                        principalTable: "nozzles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_machine_capability_nozzles_nozzle_id",
                table: "machine_capability_nozzles",
                column: "nozzle_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_capability_geometry_types");

            migrationBuilder.DropTable(
                name: "machine_capability_material_categories");

            migrationBuilder.DropTable(
                name: "machine_capability_nozzles");

            migrationBuilder.DropTable(
                name: "machine_capabilities");
        }
    }
}
