using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstituteOfFineArts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeviceTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_token",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    token = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: false),
                    platform = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_token_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_token_account",
                table: "device_token",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "UX_device_token_token",
                table: "device_token",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_token");
        }
    }
}
