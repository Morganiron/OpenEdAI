using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenEdAI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeleteBahaviorForCreatorCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Students_UserID",
                table: "Courses");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Students_UserID",
                table: "Courses",
                column: "UserID",
                principalTable: "Students",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Students_UserID",
                table: "Courses");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Students_UserID",
                table: "Courses",
                column: "UserID",
                principalTable: "Students",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
