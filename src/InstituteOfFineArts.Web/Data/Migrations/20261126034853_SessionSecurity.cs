using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstituteOfFineArts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "account",
                type: "varchar(36)",
                unicode: false,
                maxLength: 36,
                nullable: false,
                defaultValueSql: "LOWER(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''))");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_username_action",
                table: "audit_log",
                columns: new[] { "username", "action", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_log_username_action",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "account");
        }
    }
}
