using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstituteOfFineArts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class JuryCriteriaImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "total_score",
                table: "evaluation",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "competition_judge",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<long>(type: "bigint", nullable: false),
                    judge_id = table.Column<long>(type: "bigint", nullable: false),
                    is_chair = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_judge", x => x.id);
                    table.ForeignKey(
                        name: "fk_competition_judge_competition_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_competition_judge_staff_profiles_judge_id",
                        column: x => x.judge_id,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grading_criterion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grading_criterion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "judge_evaluation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    submission_id = table.Column<long>(type: "bigint", nullable: false),
                    judge_id = table.Column<long>(type: "bigint", nullable: false),
                    disqualified = table.Column<bool>(type: "bit", nullable: false),
                    total_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    positive_points = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    negative_points = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    improvement_areas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_judge_evaluation", x => x.id);
                    table.CheckConstraint("CK_judge_evaluation_dq", "[disqualified] = 0 OR ([negative_points] IS NOT NULL AND [total_score] IS NULL)");
                    table.CheckConstraint("CK_judge_evaluation_total", "[total_score] IS NULL OR ([total_score] >= 0 AND [total_score] <= 100)");
                    table.ForeignKey(
                        name: "fk_judge_evaluation_staff_profiles_judge_id",
                        column: x => x.judge_id,
                        principalTable: "staff_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_judge_evaluation_submissions_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rating_threshold",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rating = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    min_score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rating_threshold", x => x.id);
                    table.CheckConstraint("CK_rating_threshold_rating", "[rating] IN ('BEST','BETTER','GOOD','MODERATE')");
                    table.CheckConstraint("CK_rating_threshold_score", "[min_score] >= 0 AND [min_score] <= 100");
                });

            migrationBuilder.CreateTable(
                name: "submission_image",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    submission_id = table.Column<long>(type: "bigint", nullable: false),
                    image_path = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    thumbnail_path = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    original_file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    is_cover = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_image", x => x.id);
                    table.CheckConstraint("CK_submission_image_type", "[content_type] IN ('image/jpeg','image/png','image/webp')");
                    table.ForeignKey(
                        name: "fk_submission_image_submission_submission_id",
                        column: x => x.submission_id,
                        principalTable: "submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "competition_criterion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    competition_id = table.Column<long>(type: "bigint", nullable: false),
                    criterion_id = table.Column<long>(type: "bigint", nullable: false),
                    weight_percent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_criterion", x => x.id);
                    table.CheckConstraint("CK_competition_criterion_weight", "[weight_percent] > 0 AND [weight_percent] <= 100");
                    table.ForeignKey(
                        name: "fk_competition_criterion_competition_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_competition_criterion_grading_criteria_criterion_id",
                        column: x => x.criterion_id,
                        principalTable: "grading_criterion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "judge_score",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    judge_evaluation_id = table.Column<long>(type: "bigint", nullable: false),
                    competition_criterion_id = table.Column<long>(type: "bigint", nullable: false),
                    score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "SYSDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_judge_score", x => x.id);
                    table.CheckConstraint("CK_judge_score_range", "[score] >= 0 AND [score] <= 100");
                    table.ForeignKey(
                        name: "fk_judge_score_competition_criterion_competition_criterion_id",
                        column: x => x.competition_criterion_id,
                        principalTable: "competition_criterion",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_judge_score_judge_evaluation_judge_evaluation_id",
                        column: x => x.judge_evaluation_id,
                        principalTable: "judge_evaluation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_competition_criterion_criterion_id",
                table: "competition_criterion",
                column: "criterion_id");

            migrationBuilder.CreateIndex(
                name: "UX_competition_criterion",
                table: "competition_criterion",
                columns: new[] { "competition_id", "criterion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_competition_judge_judge",
                table: "competition_judge",
                column: "judge_id");

            migrationBuilder.CreateIndex(
                name: "UX_competition_judge",
                table: "competition_judge",
                columns: new[] { "competition_id", "judge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_grading_criterion_name",
                table: "grading_criterion",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_judge_evaluation_judge",
                table: "judge_evaluation",
                column: "judge_id");

            migrationBuilder.CreateIndex(
                name: "UX_judge_evaluation",
                table: "judge_evaluation",
                columns: new[] { "submission_id", "judge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_judge_score_competition_criterion_id",
                table: "judge_score",
                column: "competition_criterion_id");

            migrationBuilder.CreateIndex(
                name: "UX_judge_score",
                table: "judge_score",
                columns: new[] { "judge_evaluation_id", "competition_criterion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_rating_threshold_rating",
                table: "rating_threshold",
                column: "rating",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_image_order",
                table: "submission_image",
                columns: new[] { "submission_id", "sort_order" });
            // Entries saved before pictures were kept one by one: their single image becomes their only picture (the cover)
            migrationBuilder.Sql(@"
INSERT INTO submission_image (submission_id, image_path, thumbnail_path, original_file_name, file_size, content_type, sort_order, is_cover, created_at)
SELECT s.id, s.image_path, s.thumbnail_path, s.original_file_name, s.file_size,
       CASE LOWER(RIGHT(s.image_path, 5)) WHEN '.webp' THEN 'image/webp'
            ELSE CASE LOWER(RIGHT(s.image_path, 4)) WHEN '.png' THEN 'image/png' ELSE 'image/jpeg' END END,
       0, 1, SYSDATETIME()
FROM submission s
WHERE NOT EXISTS (SELECT 1 FROM submission_image i WHERE i.submission_id = s.id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_judge");

            migrationBuilder.DropTable(
                name: "judge_score");

            migrationBuilder.DropTable(
                name: "rating_threshold");

            migrationBuilder.DropTable(
                name: "submission_image");

            migrationBuilder.DropTable(
                name: "competition_criterion");

            migrationBuilder.DropTable(
                name: "judge_evaluation");

            migrationBuilder.DropTable(
                name: "grading_criterion");

            migrationBuilder.DropColumn(
                name: "total_score",
                table: "evaluation");
        }
    }
}
