using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProductionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    category = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    grade = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materials", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "nozzles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    type = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    diameter_millimeters = table.Column<decimal>(
                        type: "numeric(10,3)",
                        precision: 10,
                        scale: 3,
                        nullable: false
                    ),
                    maximum_pressure_bar = table.Column<decimal>(
                        type: "numeric(10,3)",
                        precision: 10,
                        scale: 3,
                        nullable: false
                    ),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    wear_percentage = table.Column<decimal>(
                        type: "numeric(5,2)",
                        precision: 5,
                        scale: 2,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nozzles", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_materials_code",
                table: "materials",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_nozzles_code",
                table: "nozzles",
                column: "code",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "materials");

            migrationBuilder.DropTable(name: "nozzles");
        }
    }
}
