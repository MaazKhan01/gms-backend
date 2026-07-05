using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNationalitiesAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuestSessions_Guests_GuestId1",
                table: "GuestSessions");

            migrationBuilder.DropIndex(
                name: "IX_GuestSessions_GuestId1",
                table: "GuestSessions");

            migrationBuilder.DropColumn(
                name: "GuestId1",
                table: "GuestSessions");

            migrationBuilder.AlterColumn<string>(
                name: "GuestType",
                table: "Guests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateTable(
                name: "InvitationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubjectAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetTiers = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true, defaultValueSql: "((0))"),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitationTemplates_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Nationalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Flag = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nationalities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guests_InvitationTemplateId",
                table: "Guests",
                column: "InvitationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_NationalityId",
                table: "Guests",
                column: "NationalityId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationTemplates_EventId",
                table: "InvitationTemplates",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Nationalities_Code",
                table: "Nationalities",
                column: "Code",
                unique: true);

            // Null out any orphaned FK values — InvitationTemplates/Nationalities tables
            // were just created and are empty, so any pre-existing non-null value would
            // violate the FK constraint.
            migrationBuilder.Sql("UPDATE Guests SET InvitationTemplateId = NULL WHERE InvitationTemplateId IS NOT NULL");
            migrationBuilder.Sql("UPDATE Guests SET NationalityId = NULL WHERE NationalityId IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_InvitationTemplates_InvitationTemplateId",
                table: "Guests",
                column: "InvitationTemplateId",
                principalTable: "InvitationTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Nationalities_NationalityId",
                table: "Guests",
                column: "NationalityId",
                principalTable: "Nationalities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_InvitationTemplates_InvitationTemplateId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Nationalities_NationalityId",
                table: "Guests");

            migrationBuilder.DropTable(
                name: "InvitationTemplates");

            migrationBuilder.DropTable(
                name: "Nationalities");

            migrationBuilder.DropIndex(
                name: "IX_Guests_InvitationTemplateId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_NationalityId",
                table: "Guests");

            migrationBuilder.AddColumn<Guid>(
                name: "GuestId1",
                table: "GuestSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuestType",
                table: "Guests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestSessions_GuestId1",
                table: "GuestSessions",
                column: "GuestId1");

            migrationBuilder.AddForeignKey(
                name: "FK_GuestSessions_Guests_GuestId1",
                table: "GuestSessions",
                column: "GuestId1",
                principalTable: "Guests",
                principalColumn: "Id");
        }
    }
}
