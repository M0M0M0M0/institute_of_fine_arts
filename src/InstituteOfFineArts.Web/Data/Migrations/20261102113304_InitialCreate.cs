using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstituteOfFineArts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<string>(type: "char(1)", nullable: true),
                    address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account", x => x.id);
                    table.CheckConstraint("CK_account_gender", "[gender] IS NULL OR [gender] IN ('M','F','O')");
                    table.CheckConstraint("CK_account_role", "[role] IN ('ADMIN','MANAGER','STAFF','STUDENT')");
                });

            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    full_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_profile",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    staff_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    joined_date = table.Column<DateOnly>(type: "date", nullable: false),
                    classes_in_charge = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    subjects_in_charge = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    qualification = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_profile_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_profile",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    student_code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    enrollment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    course_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    class_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    enrollment_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_profile", x => x.id);
                    table.CheckConstraint("CK_student_profile_status", "[enrollment_status] IN ('STUDYING','GRADUATED','SUSPENDED','DROPPED')");
                    table.ForeignKey(
                        name: "fk_student_profile_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competition",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    published_by = table.Column<long>(type: "bigint", nullable: true),
                    cover_image_path = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    result_remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition", x => x.id);
                    table.CheckConstraint("CK_competition_dates", "[end_date] >= [start_date]");
                    table.CheckConstraint("CK_competition_status", "[status] IN ('UPCOMING','ONGOING','CLOSED','GRADED')");
                    table.ForeignKey(
                        name: "fk_competition_staff_profiles_created_by",
                        column: x => x.created_by,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_competition_staff_profiles_published_by",
                        column: x => x.published_by,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exhibition",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    venue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    city = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    organizer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exhibition", x => x.id);
                    table.CheckConstraint("CK_exhibition_dates", "[end_date] >= [start_date]");
                    table.CheckConstraint("CK_exhibition_status", "[status] IN ('UPCOMING','ONGOING','FINISHED')");
                    table.ForeignKey(
                        name: "fk_exhibition_staff_profiles_created_by",
                        column: x => x.created_by,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competition_award",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<long>(type: "bigint", nullable: false),
                    award_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    rank_no = table.Column<int>(type: "int", nullable: false),
                    prize_value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    prize_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_award", x => x.id);
                    table.CheckConstraint("CK_competition_award_prize", "[prize_value] >= 0");
                    table.CheckConstraint("CK_competition_award_quantity", "[quantity] > 0");
                    table.CheckConstraint("CK_competition_award_rank", "[rank_no] >= 1");
                    table.ForeignKey(
                        name: "fk_competition_award_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<long>(type: "bigint", nullable: false),
                    student_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    image_path = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    thumbnail_path = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    original_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    text_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    text_content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission", x => x.id);
                    table.CheckConstraint("CK_submission_size", "[file_size] > 0");
                    table.CheckConstraint("CK_submission_text_type", "[text_type] IN ('POEM','QUOTATION','STORY','OTHER')");
                    table.ForeignKey(
                        name: "fk_submission_competition_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_student_profile_student_id",
                        column: x => x.student_id,
                        principalTable: "student_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "award_issued",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_award_id = table.Column<long>(type: "bigint", nullable: false),
                    submission_id = table.Column<long>(type: "bigint", nullable: false),
                    issued_by = table.Column<long>(type: "bigint", nullable: false),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: false, defaultValueSql: "CAST(SYSDATETIME() AS date)"),
                    remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_award_issued", x => x.id);
                    table.ForeignKey(
                        name: "fk_award_issued_competition_awards_competition_award_id",
                        column: x => x.competition_award_id,
                        principalTable: "competition_award",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_award_issued_staff_profiles_issued_by",
                        column: x => x.issued_by,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_award_issued_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    submission_id = table.Column<long>(type: "bigint", nullable: false),
                    evaluated_by = table.Column<long>(type: "bigint", nullable: false),
                    rating = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    positive_points = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    negative_points = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    improvement_areas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation", x => x.id);
                    table.CheckConstraint("CK_evaluation_dq_reason", "[rating] <> 'DISQUALIFIED' OR [negative_points] IS NOT NULL");
                    table.CheckConstraint("CK_evaluation_rating", "[rating] IN ('BEST','BETTER','GOOD','MODERATE','NORMAL','DISQUALIFIED')");
                    table.ForeignKey(
                        name: "fk_evaluation_staff_profiles_evaluated_by",
                        column: x => x.evaluated_by,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exhibition_entry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    exhibition_id = table.Column<long>(type: "bigint", nullable: false),
                    submission_id = table.Column<long>(type: "bigint", nullable: false),
                    asking_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    reject_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    sold_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    sold_date = table.Column<DateOnly>(type: "date", nullable: true),
                    customer_id = table.Column<long>(type: "bigint", nullable: true),
                    paid_to_student = table.Column<bool>(type: "bit", nullable: false),
                    paid_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    proposed_by = table.Column<long>(type: "bigint", nullable: false),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exhibition_entry", x => x.id);
                    table.CheckConstraint("CK_entry_asking_price", "[asking_price] > 0");
                    table.CheckConstraint("CK_entry_paid", "[paid_to_student] = 0 OR ([status] = 'SOLD' AND [paid_amount] IS NOT NULL AND [paid_date] IS NOT NULL)");
                    table.CheckConstraint("CK_entry_paid_amount", "[paid_amount] IS NULL OR [paid_amount] >= 0");
                    table.CheckConstraint("CK_entry_paid_date", "[paid_date] IS NULL OR [sold_date] IS NULL OR [paid_date] >= [sold_date]");
                    table.CheckConstraint("CK_entry_reject", "[status] <> 'REJECTED' OR [reject_reason] IS NOT NULL");
                    table.CheckConstraint("CK_entry_sold", "[status] <> 'SOLD' OR ([sold_price] IS NOT NULL AND [sold_date] IS NOT NULL AND [customer_id] IS NOT NULL)");
                    table.CheckConstraint("CK_entry_sold_price", "[sold_price] IS NULL OR [sold_price] > 0");
                    table.CheckConstraint("CK_entry_status", "[status] IN ('PENDING','ACCEPTED','REJECTED','SOLD','UNSOLD')");
                    table.ForeignKey(
                        name: "fk_exhibition_entry_account_proposed_by",
                        column: x => x.proposed_by,
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exhibition_entry_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exhibition_entry_exhibition_exhibition_id",
                        column: x => x.exhibition_id,
                        principalTable: "exhibition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exhibition_entry_staff_profiles_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exhibition_entry_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_role",
                table: "account",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "UX_account_email",
                table: "account",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_account_username",
                table: "account",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_award_issued_competition_award_id",
                table: "award_issued",
                column: "competition_award_id");

            migrationBuilder.CreateIndex(
                name: "ix_award_issued_issued_by",
                table: "award_issued",
                column: "issued_by");

            migrationBuilder.CreateIndex(
                name: "UX_award_issued_submission",
                table: "award_issued",
                column: "submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_created_by",
                table: "competition",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_competition_published_by",
                table: "competition",
                column: "published_by");

            migrationBuilder.CreateIndex(
                name: "IX_competition_status_dates",
                table: "competition",
                columns: new[] { "status", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "UX_competition_code",
                table: "competition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_competition_award_name",
                table: "competition_award",
                columns: new[] { "competition_id", "award_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_phone",
                table: "customer",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_evaluated_by",
                table: "evaluation",
                column: "evaluated_by");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_rating",
                table: "evaluation",
                column: "rating");

            migrationBuilder.CreateIndex(
                name: "UX_evaluation_submission",
                table: "evaluation",
                column: "submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_created_by",
                table: "exhibition",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_entry_exhibition_status",
                table: "exhibition_entry",
                columns: new[] { "exhibition_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_entry_customer_id",
                table: "exhibition_entry",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_entry_proposed_by",
                table: "exhibition_entry",
                column: "proposed_by");

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_entry_reviewed_by",
                table: "exhibition_entry",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "UX_entry_active_submission",
                table: "exhibition_entry",
                column: "submission_id",
                unique: true,
                filter: "[status] IN ('PENDING','ACCEPTED')");

            migrationBuilder.CreateIndex(
                name: "UX_staff_profile_account",
                table: "staff_profile",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_staff_profile_staff_code",
                table: "staff_profile",
                column: "staff_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_profile_class_name",
                table: "student_profile",
                column: "class_name");

            migrationBuilder.CreateIndex(
                name: "UX_student_profile_account",
                table: "student_profile",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_student_profile_student_code",
                table: "student_profile",
                column: "student_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_competition_submitted",
                table: "submission",
                columns: new[] { "competition_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_submission_student",
                table: "submission",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "UX_submission_competition_student",
                table: "submission",
                columns: new[] { "competition_id", "student_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "award_issued");

            migrationBuilder.DropTable(
                name: "evaluation");

            migrationBuilder.DropTable(
                name: "exhibition_entry");

            migrationBuilder.DropTable(
                name: "competition_award");

            migrationBuilder.DropTable(
                name: "customer");

            migrationBuilder.DropTable(
                name: "exhibition");

            migrationBuilder.DropTable(
                name: "submission");

            migrationBuilder.DropTable(
                name: "competition");

            migrationBuilder.DropTable(
                name: "student_profile");

            migrationBuilder.DropTable(
                name: "staff_profile");

            migrationBuilder.DropTable(
                name: "account");
        }
    }
}
