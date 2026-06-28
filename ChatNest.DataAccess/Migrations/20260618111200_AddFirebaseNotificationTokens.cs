using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNest.DataAccess.Migrations
{
    public partial class AddFirebaseNotificationTokens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FcmTokensJson",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FcmTokensJson",
                table: "Users");
        }
    }
}
