using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstituteOfFineArts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RolesTeacherAndAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_account_role",
                table: "account");

            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "audit_log",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "audit_log",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "OTHER");

            // Rows written before this migration: the sign-in rows are the only ones we can classify
            migrationBuilder.Sql("UPDATE audit_log SET category = 'LOGIN' WHERE action IN ('LOGIN', 'LOGIN_FAILED', 'LOGOUT');");

            migrationBuilder.AddColumn<long>(
                name: "competition_id",
                table: "audit_log",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "exhibition_id",
                table: "audit_log",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "submission_id",
                table: "audit_log",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "audit_log",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "target_account_id",
                table: "audit_log",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_competition",
                table: "audit_log",
                columns: new[] { "competition_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_exhibition",
                table: "audit_log",
                columns: new[] { "exhibition_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_submission",
                table: "audit_log",
                columns: new[] { "submission_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_target_account",
                table: "audit_log",
                columns: new[] { "target_account_id", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_audit_log_category",
                table: "audit_log",
                sql: "[category] IN ('LOGIN','ACCOUNT','COMPETITION','SUBMISSION','GRADING','AWARD','EXHIBITION','SALE','OTHER')");

            // MANAGER no longer exists: its overview now belongs to STAFF (one person who also grades gets a TEACHER account)
            migrationBuilder.Sql("UPDATE account SET role = 'STAFF' WHERE role = 'MANAGER';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_account_role",
                table: "account",
                sql: "[role] IN ('ADMIN','STAFF','TEACHER','STUDENT')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_log_competition",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_exhibition",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_submission",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_target_account",
                table: "audit_log");

            migrationBuilder.DropCheckConstraint(
                name: "CK_audit_log_category",
                table: "audit_log");

            migrationBuilder.DropCheckConstraint(
                name: "CK_account_role",
                table: "account");

            // TEACHER does not exist before this migration
            migrationBuilder.Sql("UPDATE account SET role = 'STAFF' WHERE role = 'TEACHER';");

            migrationBuilder.DropColumn(
                name: "category",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "competition_id",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "exhibition_id",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "submission_id",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "target_account_id",
                table: "audit_log");

            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "audit_log",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_account_role",
                table: "account",
                sql: "[role] IN ('ADMIN','MANAGER','STAFF','STUDENT')");
        }
    }
}
