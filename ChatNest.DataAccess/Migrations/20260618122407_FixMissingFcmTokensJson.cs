using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatNest.DataAccess.Migrations
{
    public partial class FixMissingFcmTokensJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users', 'FcmTokensJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [FcmTokensJson] nvarchar(max) NOT NULL
    CONSTRAINT [DF_Users_FcmTokensJson] DEFAULT N'';
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Users', 'FcmTokensJson') IS NOT NULL
BEGIN
    DECLARE @constraintName nvarchar(200);

    SELECT @constraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo'
      AND t.name = 'Users'
      AND c.name = 'FcmTokensJson';

    IF @constraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [dbo].[Users] DROP CONSTRAINT [' + @constraintName + ']');
    END

    ALTER TABLE [dbo].[Users] DROP COLUMN [FcmTokensJson];
END
");
        }
    }
}