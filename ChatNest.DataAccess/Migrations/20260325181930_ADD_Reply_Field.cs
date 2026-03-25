using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ADD_Reply_Field : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplyToContent",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToFileName",
                table: "Messages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToSenderId",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToType",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyToContent",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToFileName",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToSenderId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Groups");
        }
    }
}
