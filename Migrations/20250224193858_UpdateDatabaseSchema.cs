using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenEdAI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompletedLessons",
                table: "CourseProgress",
                newName: "CompletedLessonsJson");

            migrationBuilder.RenameColumn(
                name: "ProgessID",
                table: "CourseProgress",
                newName: "ProgressID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompletedLessonsJson",
                table: "CourseProgress",
                newName: "CompletedLessons");

            migrationBuilder.RenameColumn(
                name: "ProgressID",
                table: "CourseProgress",
                newName: "ProgessID");
        }
    }
}
