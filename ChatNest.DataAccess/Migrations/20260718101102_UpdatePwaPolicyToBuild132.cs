using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNest.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePwaPolicyToBuild132 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppVersionPolicies",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "LatestBuild", "LatestVersion", "Message", "Title" },
                values: new object[] { 132, "2.6.1", "برای استفاده از آخرین بهبودها و رفع اشکال‌ها، برنامه را به‌روزرسانی کنید.", "نسخه جدید چت‌نست آماده است" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppVersionPolicies",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "LatestBuild", "LatestVersion", "Message", "Title" },
                values: new object[] { 130, "2.5.0", "Reload ChatNest to use the latest version.", "A new ChatNest version is available" });
        }
    }
}
