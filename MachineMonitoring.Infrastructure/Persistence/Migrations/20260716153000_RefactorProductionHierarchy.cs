using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations;

public partial class RefactorProductionHierarchy : Migration
{
    private static readonly Guid LegacyProductionLotId = Guid.Parse(
        "90000000-0000-0000-0000-000000000001"
    );

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "production_lots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                planned_quantity = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_production_lots", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "workpieces",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                production_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                material_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                is_sequence_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workpieces", x => x.id);
                table.ForeignKey(
                    name: "FK_workpieces_production_lots_production_lot_id",
                    column: x => x.production_lot_id,
                    principalTable: "production_lots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.AddColumn<int>(
            name: "sequence_number",
            table: "machine_operations",
            type: "integer",
            nullable: true
        );

        migrationBuilder.InsertData(
            table: "production_lots",
            columns: ["id", "code", "planned_quantity", "status", "created_at", "started_at", "completed_at"],
            values: new object[]
            {
                LegacyProductionLotId,
                "LOT-LEGACY",
                1,
                "Planned",
                new DateTimeOffset(new DateTime(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc)),
                null,
                null,
            }
        );

        migrationBuilder.Sql(
            $"""
            INSERT INTO workpieces (
                id,
                production_lot_id,
                code,
                material_code,
                status,
                is_sequence_active,
                created_at,
                started_at,
                completed_at
            )
            SELECT
                workpiece_id,
                '{LegacyProductionLotId}'::uuid,
                'LEGACY-' || LEFT(workpiece_id::text, 8),
                'LEGACY',
                CASE
                    WHEN bool_and(status = 'Completed') THEN 'Completed'
                    WHEN bool_or(status = 'Failed') THEN 'Failed'
                    WHEN bool_or(status = 'Running' OR status = 'Paused') THEN 'Running'
                    WHEN bool_or(status = 'Queued') THEN 'Pending'
                    WHEN bool_or(status = 'Cancelled') THEN 'Cancelled'
                    ELSE 'Pending'
                END,
                FALSE,
                MIN(created_at),
                MIN(started_at),
                CASE
                    WHEN bool_and(status IN ('Completed', 'Failed', 'Cancelled'))
                        THEN MAX(COALESCE(completed_at, started_at, created_at))
                    ELSE NULL
                END
            FROM machine_operations
            GROUP BY workpiece_id;
            """
        );

        migrationBuilder.Sql(
            """
            WITH numbered_operations AS (
                SELECT
                    id,
                    ROW_NUMBER() OVER (
                        PARTITION BY workpiece_id
                        ORDER BY created_at, id
                    ) AS sequence_number
                FROM machine_operations
            )
            UPDATE machine_operations AS target
            SET sequence_number = numbered_operations.sequence_number
            FROM numbered_operations
            WHERE target.id = numbered_operations.id;
            """
        );

        migrationBuilder.Sql(
            $"""
            UPDATE production_lots
            SET
                planned_quantity = COALESCE((
                    SELECT COUNT(*)
                    FROM workpieces
                    WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                ), 1),
                status = CASE
                    WHEN NOT EXISTS (
                        SELECT 1 FROM workpieces WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                    ) THEN 'Planned'
                    WHEN NOT EXISTS (
                        SELECT 1
                        FROM workpieces
                        WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                          AND status <> 'Completed'
                    ) THEN 'Completed'
                    WHEN NOT EXISTS (
                        SELECT 1
                        FROM workpieces
                        WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                          AND status NOT IN ('Completed', 'Failed', 'Cancelled')
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM workpieces
                        WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                          AND status IN ('Failed', 'Cancelled')
                    ) THEN 'Failed'
                    ELSE 'Running'
                END,
                started_at = (
                    SELECT MIN(started_at)
                    FROM workpieces
                    WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                ),
                completed_at = (
                    SELECT MAX(completed_at)
                    FROM workpieces
                    WHERE production_lot_id = '{LegacyProductionLotId}'::uuid
                )
            WHERE id = '{LegacyProductionLotId}'::uuid;
            """
        );

        migrationBuilder.AlterColumn<int>(
            name: "sequence_number",
            table: "machine_operations",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_production_lots_code",
            table: "production_lots",
            column: "code",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_production_lots_status",
            table: "production_lots",
            column: "status"
        );

        migrationBuilder.CreateIndex(
            name: "IX_workpieces_production_lot_id",
            table: "workpieces",
            column: "production_lot_id"
        );

        migrationBuilder.CreateIndex(
            name: "IX_workpieces_status",
            table: "workpieces",
            column: "status"
        );

        migrationBuilder.CreateIndex(
            name: "IX_machine_operations_workpiece_id_sequence_number",
            table: "machine_operations",
            columns: ["workpiece_id", "sequence_number"],
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_machine_operations_workpiece_id_status_sequence_number",
            table: "machine_operations",
            columns: ["workpiece_id", "status", "sequence_number"]
        );

        migrationBuilder.AddForeignKey(
            name: "FK_machine_operations_workpieces_workpiece_id",
            table: "machine_operations",
            column: "workpiece_id",
            principalTable: "workpieces",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_machine_operations_workpieces_workpiece_id",
            table: "machine_operations"
        );

        migrationBuilder.DropIndex(
            name: "IX_machine_operations_workpiece_id_sequence_number",
            table: "machine_operations"
        );

        migrationBuilder.DropIndex(
            name: "IX_machine_operations_workpiece_id_status_sequence_number",
            table: "machine_operations"
        );

        migrationBuilder.DropColumn(
            name: "sequence_number",
            table: "machine_operations"
        );

        migrationBuilder.DropTable(
            name: "workpieces"
        );

        migrationBuilder.DropTable(
            name: "production_lots"
        );
    }
}
