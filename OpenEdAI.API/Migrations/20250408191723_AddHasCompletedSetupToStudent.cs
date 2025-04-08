using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenEdAI.Migrations
{
    /// <inheritdoc />
    public partial class AddHasCompletedSetupToStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedSetup",
                table: "Students",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCompletedSetup",
                table: "Students");
        }
    }
}
