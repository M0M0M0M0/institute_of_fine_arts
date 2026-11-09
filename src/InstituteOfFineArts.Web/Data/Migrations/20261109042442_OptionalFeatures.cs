using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstituteOfFineArts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptionalFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: true),
                    username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    action = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    entity = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_log_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    link = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_token",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    token_hash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    used_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_password_reset_token_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_account",
                table: "audit_log",
                columns: new[] { "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_created_at",
                table: "audit_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notification_account_read",
                table: "notification",
                columns: new[] { "account_id", "is_read", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_token_account_id",
                table: "password_reset_token",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "UX_password_reset_token_hash",
                table: "password_reset_token",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "password_reset_token");
        }
    }
}
