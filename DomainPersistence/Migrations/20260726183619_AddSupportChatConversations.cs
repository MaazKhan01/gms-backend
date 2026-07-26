using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentType",
                table: "SupportMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "SupportMessages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConversationId",
                table: "SupportMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "SupportMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SenderUserId",
                table: "SupportMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupportConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    AssignedAdminUserId = table.Column<int>(type: "int", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LastMessageFromGuest = table.Column<bool>(type: "bit", nullable: false),
                    UnreadByAdminCount = table.Column<int>(type: "int", nullable: false),
                    UnreadByGuestCount = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportConversations_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportConversations_Users_AssignedAdminUserId",
                        column: x => x.AssignedAdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportConversations_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_ConversationId_SentAt",
                table: "SupportMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_SenderUserId",
                table: "SupportMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_AssignedAdminUserId",
                table: "SupportConversations",
                column: "AssignedAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_ClosedByUserId",
                table: "SupportConversations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_GuestId",
                table: "SupportConversations",
                column: "GuestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_PublicId",
                table: "SupportConversations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_Status_UnreadByAdminCount_LastMessageAt",
                table: "SupportConversations",
                columns: new[] { "Status", "UnreadByAdminCount", "LastMessageAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_SupportConversations_ConversationId",
                table: "SupportMessages",
                column: "ConversationId",
                principalTable: "SupportConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_Users_SenderUserId",
                table: "SupportMessages",
                column: "SenderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: one SupportConversation per guest who already has messages,
            // summarized from their existing SupportMessages, then point those
            // messages at the new conversation row. Safe to run repeatedly — the
            // UPDATE only touches rows still missing a ConversationId.
            migrationBuilder.Sql(@"
INSERT INTO SupportConversations (GuestId, Status, LastMessageAt, LastMessagePreview, LastMessageFromGuest, UnreadByAdminCount, UnreadByGuestCount, CreatedAt, IsDeleted, PublicId)
SELECT
    g.GuestId,
    'Open',
    agg.LastMessageAt,
    LEFT(agg.LastMessageBody, 200),
    agg.LastMessageFromGuest,
    (SELECT COUNT(*) FROM SupportMessages m2 WHERE m2.GuestId = g.GuestId AND m2.FromGuest = 1 AND m2.IsRead = 0 AND (m2.IsDeleted IS NULL OR m2.IsDeleted = 0)),
    (SELECT COUNT(*) FROM SupportMessages m2 WHERE m2.GuestId = g.GuestId AND m2.FromGuest = 0 AND m2.IsRead = 0 AND (m2.IsDeleted IS NULL OR m2.IsDeleted = 0)),
    SYSUTCDATETIME(),
    0,
    NEWID()
FROM (SELECT DISTINCT GuestId FROM SupportMessages WHERE IsDeleted IS NULL OR IsDeleted = 0) g
CROSS APPLY (
    SELECT TOP 1 m.SentAt AS LastMessageAt, m.Body AS LastMessageBody, m.FromGuest AS LastMessageFromGuest
    FROM SupportMessages m
    WHERE m.GuestId = g.GuestId AND (m.IsDeleted IS NULL OR m.IsDeleted = 0)
    ORDER BY m.SentAt DESC
) agg
WHERE NOT EXISTS (SELECT 1 FROM SupportConversations c WHERE c.GuestId = g.GuestId);

UPDATE m
SET m.ConversationId = c.Id
FROM SupportMessages m
INNER JOIN SupportConversations c ON c.GuestId = m.GuestId
WHERE m.ConversationId IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_SupportConversations_ConversationId",
                table: "SupportMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_Users_SenderUserId",
                table: "SupportMessages");

            migrationBuilder.DropTable(
                name: "SupportConversations");

            migrationBuilder.DropIndex(
                name: "IX_SupportMessages_ConversationId_SentAt",
                table: "SupportMessages");

            migrationBuilder.DropIndex(
                name: "IX_SupportMessages_SenderUserId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentType",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "SenderUserId",
                table: "SupportMessages");
        }
    }
}
