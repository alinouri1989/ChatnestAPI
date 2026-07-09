using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinnedForJson",
                table: "Chats",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinnedForJson",
                table: "Chats");
        }
    }
}
