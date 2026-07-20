using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MachineMonitoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxProcessingMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempts",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "outbox_messages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processed_at",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempts",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "processed_at",
                table: "outbox_messages");
        }
    }
}
