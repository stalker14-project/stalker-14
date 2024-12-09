using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class StalkerAddBandsAndWarZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stalker_bands",
                columns: table => new
                {
                    stalker_bands_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    band_proto_id = table.Column<string>(type: "text", nullable: false),
                    reward_points = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalker_bands", x => x.stalker_bands_id);
                });

            migrationBuilder.CreateTable(
                name: "stalker_zone_ownerships",
                columns: table => new
                {
                    stalker_zone_ownerships_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    zone_proto_id = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: true),
                    last_captured_by_current_owner_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalker_zone_ownerships", x => x.stalker_zone_ownerships_id);
                    table.ForeignKey(
                        name: "FK_stalker_zone_ownerships_stalker_bands_owner_id",
                        column: x => x.owner_id,
                        principalTable: "stalker_bands",
                        principalColumn: "stalker_bands_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stalker_zone_ownerships_owner_id",
                table: "stalker_zone_ownerships",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stalker_zone_ownerships");

            migrationBuilder.DropTable(
                name: "stalker_bands");
        }
    }
}
