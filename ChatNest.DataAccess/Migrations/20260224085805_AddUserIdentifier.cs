using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserIdentifier",
                table: "Users",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserIdentifier",
                table: "Users",
                column: "UserIdentifier",
                unique: true,
                filter: "[UserIdentifier] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_UserIdentifier",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserIdentifier",
                table: "Users");
        }
    }
}
