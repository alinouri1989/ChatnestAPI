using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ChatNest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAppVersionPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppVersionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LatestVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LatestBuild = table.Column<int>(type: "int", nullable: false),
                    MinimumSupportedVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MinimumSupportedBuild = table.Column<int>(type: "int", nullable: false),
                    Maintenance = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StoreUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RemindAfterSeconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppVersionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppVersionReleaseNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppVersionPolicyId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppVersionReleaseNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppVersionReleaseNotes_AppVersionPolicies_AppVersionPolicyId",
                        column: x => x.AppVersionPolicyId,
                        principalTable: "AppVersionPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppVersionPolicies",
                columns: new[] { "Id", "Channel", "LatestBuild", "LatestVersion", "Maintenance", "Message", "MinimumSupportedBuild", "MinimumSupportedVersion", "Platform", "RemindAfterSeconds", "StoreUrl", "Title" },
                values: new object[,]
                {
                    { 1, "production", 130, "2.5.0", false, "Update ChatNest to get the latest improvements and fixes.", 110, "2.3.0", "android", 86400, "https://play.google.com/store/apps/details?id=ir.chatnest.app", "A new ChatNest version is available" },
                    { 2, "production", 130, "2.5.0", false, "Update ChatNest to get the latest improvements and fixes.", 110, "2.3.0", "ios", 86400, "https://apps.apple.com/app/chatnest/id0000000000", "A new ChatNest version is available" },
                    { 3, "production", 130, "2.5.0", false, "Reload ChatNest to use the latest version.", 110, "2.3.0", "pwa", 86400, "https://app.chatnest.ir", "A new ChatNest version is available" }
                });

            migrationBuilder.InsertData(
                table: "AppVersionReleaseNotes",
                columns: new[] { "Id", "AppVersionPolicyId", "DisplayOrder", "Text" },
                values: new object[,]
                {
                    { 1, 1, 1, "Improved chat performance" },
                    { 2, 1, 2, "Fixed notification problems" },
                    { 3, 1, 3, "Improved application security" },
                    { 4, 2, 1, "Improved chat performance" },
                    { 5, 3, 1, "Improved chat performance" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppVersionPolicies_Platform_Channel",
                table: "AppVersionPolicies",
                columns: new[] { "Platform", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppVersionReleaseNotes_AppVersionPolicyId_DisplayOrder",
                table: "AppVersionReleaseNotes",
                columns: new[] { "AppVersionPolicyId", "DisplayOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppVersionReleaseNotes");

            migrationBuilder.DropTable(
                name: "AppVersionPolicies");
        }
    }
}
