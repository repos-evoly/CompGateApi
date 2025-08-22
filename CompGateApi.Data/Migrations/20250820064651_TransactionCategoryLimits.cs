using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompGateApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransactionCategoryLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conditionally drop FKs (only if they exist)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLogs_CheckRequests_CheckRequestId')
    ALTER TABLE [dbo].[AuditLogs] DROP CONSTRAINT [FK_AuditLogs_CheckRequests_CheckRequestId];
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLogs_Users_UserId')
    ALTER TABLE [dbo].[AuditLogs] DROP CONSTRAINT [FK_AuditLogs_Users_UserId];
");

            // Conditionally drop indexes
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes 
           WHERE name = N'IX_AuditLogs_CheckRequestId' 
             AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
    DROP INDEX [IX_AuditLogs_CheckRequestId] ON [dbo].[AuditLogs];

IF EXISTS (SELECT 1 FROM sys.indexes 
           WHERE name = N'IX_AuditLogs_UserId' 
             AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
    DROP INDEX [IX_AuditLogs_UserId] ON [dbo].[AuditLogs];
");

            // Conditionally drop columns
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns 
           WHERE Name = N'CheckRequestId' 
                        AND Object_ID = OBJECT_ID(N'[dbo].[AuditLogs]'))
                ALTER TABLE [dbo].[AuditLogs] DROP COLUMN [CheckRequestId];

            IF EXISTS (SELECT 1 FROM sys.columns 
                    WHERE Name = N'UserId' 
                        AND Object_ID = OBJECT_ID(N'[dbo].[AuditLogs]'))
                ALTER TABLE [dbo].[AuditLogs] DROP COLUMN [UserId];
            ");

            // Your actual schema addition
            migrationBuilder.AddColumn<bool>(
                name: "CountsTowardTxnLimits",
                table: "TransactionCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the flag column
            migrationBuilder.DropColumn(
                name: "CountsTowardTxnLimits",
                table: "TransactionCategories");

            // Re-add columns only if missing
            migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.columns 
                        WHERE Name = N'CheckRequestId' 
                            AND Object_ID = OBJECT_ID(N'[dbo].[AuditLogs]'))
                ALTER TABLE [dbo].[AuditLogs] ADD [CheckRequestId] int NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.columns 
                        WHERE Name = N'UserId' 
                            AND Object_ID = OBJECT_ID(N'[dbo].[AuditLogs]'))
                ALTER TABLE [dbo].[AuditLogs] ADD [UserId] int NULL;
            ");

            // Recreate indexes only if missing
            migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                        WHERE name = N'IX_AuditLogs_CheckRequestId' 
                            AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
                CREATE INDEX [IX_AuditLogs_CheckRequestId] ON [dbo].[AuditLogs]([CheckRequestId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                        WHERE name = N'IX_AuditLogs_UserId' 
                            AND object_id = OBJECT_ID(N'[dbo].[AuditLogs]'))
                CREATE INDEX [IX_AuditLogs_UserId] ON [dbo].[AuditLogs]([UserId]);
            ");

            // Recreate FKs only if missing
            migrationBuilder.Sql(@"
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys 
                        WHERE name = N'FK_AuditLogs_CheckRequests_CheckRequestId')
                ALTER TABLE [dbo].[AuditLogs]  WITH CHECK ADD 
                    CONSTRAINT [FK_AuditLogs_CheckRequests_CheckRequestId] 
                    FOREIGN KEY([CheckRequestId]) REFERENCES [dbo].[CheckRequests]([Id]);

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys 
                        WHERE name = N'FK_AuditLogs_Users_UserId')
                ALTER TABLE [dbo].[AuditLogs]  WITH CHECK ADD 
                    CONSTRAINT [FK_AuditLogs_Users_UserId] 
                    FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([Id]);
            ");
        }

    }
}
