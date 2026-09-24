using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class DmsMissionDomainAndRoleAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserModuleGrants");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Code",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_EventId",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "LogoDarkUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ThemeAccent",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ThemeSecondary",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArrivalDate",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "DepartureDate",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "EventGuests");

            // Hand-corrected: the scaffolder guesses these as RENAMES
            // (Theme->HostName, LogoLightUrl->AttachmentUrl) because a column
            // vanished and a compatible one appeared on the same table. Neither is
            // a rename — an event's theme is not the host's name and a logo URL is
            // not an invitation attachment. Left as renames this would silently
            // carry the old values into the new columns on any database holding
            // data. Re-apply if this migration is regenerated; EF guesses the same
            // way every time.
            migrationBuilder.DropColumn(
                name: "Theme",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LogoLightUrl",
                table: "Events");

            migrationBuilder.AddColumn<string>(
                name: "HostName",
                table: "Events",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDelegateRole",
                table: "Roles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanRead",
                table: "RolePermissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanWrite",
                table: "RolePermissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Permissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Permissions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Permissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Permissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Permissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Permissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Guests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentGrade",
                table: "Guests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "Guests",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PassportExpiry",
                table: "Guests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumber",
                table: "Guests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DelegationCap",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DestinationId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HostEmail",
                table: "Events",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HostInvitationId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrVerificationNote",
                table: "EventGuests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrVerificationStatus",
                table: "EventGuests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                defaultValue: "pending");

            migrationBuilder.AddColumn<int>(
                name: "HrVerifiedBy",
                table: "EventGuests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HrVerifiedOn",
                table: "EventGuests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceStatus",
                table: "EventGuests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MissionRoleId",
                table: "EventGuests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NominatedBy",
                table: "EventGuests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NominatedOn",
                table: "EventGuests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subgroup",
                table: "EventGuests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisaRequired",
                table: "EventGuests",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "VisaStatus",
                table: "EventGuests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                defaultValue: "pending");

            migrationBuilder.CreateTable(
                name: "CombinedReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    ContentHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssembledOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_CombinedReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombinedReports_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombinedReports_Users_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CombinedReports_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    DecisionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DecidedBy = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
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
                    table.PrimaryKey("PK_FieldDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldDecisions_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldDecisions_Users_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GatheringNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Subgroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    SentBy = table.Column<int>(type: "int", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
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
                    table.PrimaryKey("PK_GatheringNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringNotifications_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringNotifications_Users_SentBy",
                        column: x => x.SentBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HostInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HostEmail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HostOrganization = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MissionTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DestinationId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HeadcountCap = table.Column<int>(type: "int", nullable: false),
                    ResponseDeadline = table.Column<DateOnly>(type: "date", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "logged"),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConvertedEventId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_HostInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostInvitations_Events_ConvertedEventId",
                        column: x => x.ConvertedEventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostInvitations_Locations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    EventGuestId = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "open"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RaisedBy = table.Column<int>(type: "int", nullable: true),
                    RaisedVia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "portal"),
                    ResolvedBy = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_EventGuests_EventGuestId",
                        column: x => x.EventGuestId,
                        principalTable: "EventGuests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_RaisedBy",
                        column: x => x.RaisedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Users_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NominationLetters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "not_generated"),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    GeneratedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HostNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_NominationLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NominationLetters_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostMissionReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventGuestId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "not_submitted"),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NudgeCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastNudgeOn = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_PostMissionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostMissionReports_EventGuests_EventGuestId",
                        column: x => x.EventGuestId,
                        principalTable: "EventGuests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessWaivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventGuestId = table.Column<int>(type: "int", nullable: false),
                    ItemKey = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WaivedBy = table.Column<int>(type: "int", nullable: true),
                    WaivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
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
                    table.PrimaryKey("PK_ReadinessWaivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessWaivers_EventGuests_EventGuestId",
                        column: x => x.EventGuestId,
                        principalTable: "EventGuests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadinessWaivers_Users_WaivedBy",
                        column: x => x.WaivedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NominationLetterHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NominationLetterId = table.Column<int>(type: "int", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_NominationLetterHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NominationLetterHistory_NominationLetters_NominationLetterId",
                        column: x => x.NominationLetterId,
                        principalTable: "NominationLetters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NominationLetterHistory_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NominationLetterVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NominationLetterId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    GeneratedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DocumentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RosterSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_NominationLetterVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NominationLetterVersions_NominationLetters_NominationLetterId",
                        column: x => x.NominationLetterId,
                        principalTable: "NominationLetters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ParentId_SortOrder",
                table: "Permissions",
                columns: new[] { "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DepartmentId",
                table: "Guests",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_DestinationId",
                table: "Events",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HostInvitationId",
                table: "Events",
                column: "HostInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_EventId_Subgroup",
                table: "EventGuests",
                columns: new[] { "EventId", "Subgroup" });

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_HrVerifiedBy",
                table: "EventGuests",
                column: "HrVerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_MissionRoleId",
                table: "EventGuests",
                column: "MissionRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_NominatedBy",
                table: "EventGuests",
                column: "NominatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CombinedReports_ApprovedBy",
                table: "CombinedReports",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CombinedReports_EventId",
                table: "CombinedReports",
                column: "EventId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CombinedReports_PublicId",
                table: "CombinedReports",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombinedReports_ReviewedBy",
                table: "CombinedReports",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_PublicId",
                table: "Departments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDecisions_DecidedBy",
                table: "FieldDecisions",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FieldDecisions_EventId",
                table: "FieldDecisions",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldDecisions_PublicId",
                table: "FieldDecisions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringNotifications_EventId",
                table: "GatheringNotifications",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringNotifications_PublicId",
                table: "GatheringNotifications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringNotifications_SentBy",
                table: "GatheringNotifications",
                column: "SentBy");

            migrationBuilder.CreateIndex(
                name: "IX_HostInvitations_ConvertedEventId",
                table: "HostInvitations",
                column: "ConvertedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_HostInvitations_DestinationId",
                table: "HostInvitations",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_HostInvitations_PublicId",
                table: "HostInvitations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_EventGuestId",
                table: "Incidents",
                column: "EventGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_EventId_Status",
                table: "Incidents",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_PublicId",
                table: "Incidents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_RaisedBy",
                table: "Incidents",
                column: "RaisedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ResolvedBy",
                table: "Incidents",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetterHistory_ActorId",
                table: "NominationLetterHistory",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetterHistory_NominationLetterId_OccurredOn",
                table: "NominationLetterHistory",
                columns: new[] { "NominationLetterId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetterHistory_PublicId",
                table: "NominationLetterHistory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetters_EventId",
                table: "NominationLetters",
                column: "EventId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetters_PublicId",
                table: "NominationLetters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetterVersions_NominationLetterId_Version",
                table: "NominationLetterVersions",
                columns: new[] { "NominationLetterId", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NominationLetterVersions_PublicId",
                table: "NominationLetterVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostMissionReports_EventGuestId",
                table: "PostMissionReports",
                column: "EventGuestId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PostMissionReports_PublicId",
                table: "PostMissionReports",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessWaivers_EventGuestId_ItemKey",
                table: "ReadinessWaivers",
                columns: new[] { "EventGuestId", "ItemKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessWaivers_PublicId",
                table: "ReadinessWaivers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessWaivers_WaivedBy",
                table: "ReadinessWaivers",
                column: "WaivedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_EventGuests_Roles_MissionRoleId",
                table: "EventGuests",
                column: "MissionRoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventGuests_Users_HrVerifiedBy",
                table: "EventGuests",
                column: "HrVerifiedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventGuests_Users_NominatedBy",
                table: "EventGuests",
                column: "NominatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_HostInvitations_HostInvitationId",
                table: "Events",
                column: "HostInvitationId",
                principalTable: "HostInvitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Locations_DestinationId",
                table: "Events",
                column: "DestinationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Departments_DepartmentId",
                table: "Guests",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Permissions_ParentId",
                table: "Permissions",
                column: "ParentId",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventGuests_Roles_MissionRoleId",
                table: "EventGuests");

            migrationBuilder.DropForeignKey(
                name: "FK_EventGuests_Users_HrVerifiedBy",
                table: "EventGuests");

            migrationBuilder.DropForeignKey(
                name: "FK_EventGuests_Users_NominatedBy",
                table: "EventGuests");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_HostInvitations_HostInvitationId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Locations_DestinationId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Guests_Departments_DepartmentId",
                table: "Guests");

            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Permissions_ParentId",
                table: "Permissions");

            migrationBuilder.DropTable(
                name: "CombinedReports");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "FieldDecisions");

            migrationBuilder.DropTable(
                name: "GatheringNotifications");

            migrationBuilder.DropTable(
                name: "HostInvitations");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "NominationLetterHistory");

            migrationBuilder.DropTable(
                name: "NominationLetterVersions");

            migrationBuilder.DropTable(
                name: "PostMissionReports");

            migrationBuilder.DropTable(
                name: "ReadinessWaivers");

            migrationBuilder.DropTable(
                name: "NominationLetters");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Code",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_ParentId_SortOrder",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Guests_DepartmentId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Events_DestinationId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_HostInvitationId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_EventId_Subgroup",
                table: "EventGuests");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_HrVerifiedBy",
                table: "EventGuests");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_MissionRoleId",
                table: "EventGuests");

            migrationBuilder.DropIndex(
                name: "IX_EventGuests_NominatedBy",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "IsDelegateRole",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CanRead",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "CanWrite",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "EmploymentGrade",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "PassportExpiry",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "DelegationCap",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "DestinationId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "HostEmail",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "HostInvitationId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "HrVerificationNote",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "HrVerificationStatus",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "HrVerifiedBy",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "HrVerifiedOn",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "InsuranceStatus",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "MissionRoleId",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "NominatedBy",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "NominatedOn",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "Subgroup",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "VisaRequired",
                table: "EventGuests");

            migrationBuilder.DropColumn(
                name: "VisaStatus",
                table: "EventGuests");

            // Mirror of the hand-correction in Up(): drop and re-add rather than
            // rename back, so rolling back does not move host names into the old
            // Theme column.
            migrationBuilder.DropColumn(
                name: "HostName",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "Events");

            migrationBuilder.AddColumn<string>(
                name: "Theme",
                table: "Events",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoLightUrl",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "Permissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoDarkUrl",
                table: "Events",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeAccent",
                table: "Events",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeSecondary",
                table: "Events",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ArrivalDate",
                table: "EventGuests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DepartureDate",
                table: "EventGuests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "EventGuests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserModuleGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(sysutcdatetime())"),
                    GrantedBy = table.Column<int>(type: "int", nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModuleGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModuleGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventGuests_EventId",
                table: "EventGuests",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleGrants_PublicId",
                table: "UserModuleGrants",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleGrants_UserId_Module",
                table: "UserModuleGrants",
                columns: new[] { "UserId", "Module" },
                unique: true);
        }
    }
}
