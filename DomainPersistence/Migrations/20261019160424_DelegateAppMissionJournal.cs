using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class DelegateAppMissionJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MissionJournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventGuestId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    IncludedInReport = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_MissionJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionJournalEntries_EventGuests_EventGuestId",
                        column: x => x.EventGuestId,
                        principalTable: "EventGuests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionJournalEntries_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissionJournalEntries_EventGuestId_EntryDate",
                table: "MissionJournalEntries",
                columns: new[] { "EventGuestId", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionJournalEntries_PublicId",
                table: "MissionJournalEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MissionJournalEntries_SessionId",
                table: "MissionJournalEntries",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionJournalEntries");
        }
    }
}
