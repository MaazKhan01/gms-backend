using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainPersistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueSeating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VenueBlocks_Venues_VenueId",
                table: "VenueBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueLayouts_VenueLayoutProps_VenuePropsId",
                table: "VenueLayouts");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueLayouts_Venues_VenueId",
                table: "VenueLayouts");

            migrationBuilder.DropIndex(
                name: "IX_VenueBlocks_VenueId",
                table: "VenueBlocks");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "VenueBlocks");

            migrationBuilder.RenameColumn(
                name: "VenuePropsId",
                table: "VenueLayouts",
                newName: "VenueBlockId");

            migrationBuilder.RenameColumn(
                name: "VenueId",
                table: "VenueLayouts",
                newName: "VenueBoxId");

            migrationBuilder.RenameIndex(
                name: "IX_VenueLayouts_VenuePropsId",
                table: "VenueLayouts",
                newName: "IX_VenueLayouts_VenueBlockId");

            migrationBuilder.RenameIndex(
                name: "IX_VenueLayouts_VenueId",
                table: "VenueLayouts",
                newName: "IX_VenueLayouts_VenueBoxId");

            migrationBuilder.RenameColumn(
                name: "Seats",
                table: "VenueLayoutProps",
                newName: "SeatsQuantity");

            migrationBuilder.RenameColumn(
                name: "RowName",
                table: "VenueLayoutProps",
                newName: "RowNames");

            migrationBuilder.AlterColumn<Guid>(
                name: "TypeId",
                table: "Venues",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Venues",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Venues",
                type: "bit",
                nullable: true,
                defaultValueSql: "((0))",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Venues",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(sysutcdatetime())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Venues",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Venues",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<double>(
                name: "Y",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "X",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "VenueLayouts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "ScaleY",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "ScaleX",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Rotation",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "OffsetY",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "OffsetX",
                table: "VenueLayouts",
                type: "float",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "VenueLayouts",
                type: "bit",
                nullable: true,
                defaultValueSql: "((0))",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VenueLayouts",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(sysutcdatetime())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "VenueLayouts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<double>(
                name: "StageW",
                table: "VenueLayoutProps",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "StageH",
                table: "VenueLayoutProps",
                type: "float",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PitchW",
                table: "VenueLayoutProps",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PitchH",
                table: "VenueLayoutProps",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "VenueLayoutProps",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "VenueLayoutProps",
                type: "bit",
                nullable: true,
                defaultValueSql: "((0))",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VenueLayoutProps",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(sysutcdatetime())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "VenueLayoutProps",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "VenueLayoutProps",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "VenueLayoutProps",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueLayoutId",
                table: "VenueLayoutProps",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "VenueBlocks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "VenueBlocks",
                type: "bit",
                nullable: true,
                defaultValueSql: "((0))",
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VenueBlocks",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(sysutcdatetime())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "VenueBlocks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "VenueBlocks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "VenueBoxId",
                table: "VenueBlocks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeatProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Index = table.Column<int>(type: "int", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VenueLayoutPropId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SeatProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatProperties_VenueLayoutProps_VenueLayoutPropId",
                        column: x => x.VenueLayoutPropId,
                        principalTable: "VenueLayoutProps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenueBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_VenueBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueBoxes_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VenueBoxes_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VenueBoxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_Seatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seatings_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seatings_Sessions_EventSessionId",
                        column: x => x.EventSessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seatings_VenueBoxes_VenueBoxId",
                        column: x => x.VenueBoxId,
                        principalTable: "VenueBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Seatings_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeatAssigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    SeatingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_SeatAssigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatAssigns_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeatAssigns_SeatProperties_SeatId",
                        column: x => x.SeatId,
                        principalTable: "SeatProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeatAssigns_Seatings_SeatingId",
                        column: x => x.SeatingId,
                        principalTable: "Seatings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Venues_TypeId",
                table: "Venues",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueLayoutProps_VenueLayoutId",
                table: "VenueLayoutProps",
                column: "VenueLayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueBlocks_VenueBoxId",
                table: "VenueBlocks",
                column: "VenueBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_VenueId",
                table: "Sessions",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatAssigns_GuestId",
                table: "SeatAssigns",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatAssigns_SeatId",
                table: "SeatAssigns",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatAssigns_SeatingId_SeatId",
                table: "SeatAssigns",
                columns: new[] { "SeatingId", "SeatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Seatings_EventId",
                table: "Seatings",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Seatings_EventSessionId",
                table: "Seatings",
                column: "EventSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Seatings_VenueBoxId",
                table: "Seatings",
                column: "VenueBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_Seatings_VenueId",
                table: "Seatings",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatProperties_VenueLayoutPropId",
                table: "SeatProperties",
                column: "VenueLayoutPropId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueBoxes_EventId",
                table: "VenueBoxes",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueBoxes_VenueId",
                table: "VenueBoxes",
                column: "VenueId");

            // Clear stale/empty VenueId values on existing sessions so the new FK can be created.
            migrationBuilder.Sql(
                "UPDATE [Sessions] SET [VenueId] = NULL " +
                "WHERE [VenueId] IS NOT NULL AND [VenueId] NOT IN (SELECT [Id] FROM [Venues]);");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Venues_VenueId",
                table: "Sessions",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VenueBlocks_VenueBoxes_VenueBoxId",
                table: "VenueBlocks",
                column: "VenueBoxId",
                principalTable: "VenueBoxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VenueLayoutProps_VenueLayouts_VenueLayoutId",
                table: "VenueLayoutProps",
                column: "VenueLayoutId",
                principalTable: "VenueLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VenueLayouts_VenueBlocks_VenueBlockId",
                table: "VenueLayouts",
                column: "VenueBlockId",
                principalTable: "VenueBlocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VenueLayouts_VenueBoxes_VenueBoxId",
                table: "VenueLayouts",
                column: "VenueBoxId",
                principalTable: "VenueBoxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Venues_LookupItems_TypeId",
                table: "Venues",
                column: "TypeId",
                principalTable: "LookupItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Venues_VenueId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueBlocks_VenueBoxes_VenueBoxId",
                table: "VenueBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueLayoutProps_VenueLayouts_VenueLayoutId",
                table: "VenueLayoutProps");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueLayouts_VenueBlocks_VenueBlockId",
                table: "VenueLayouts");

            migrationBuilder.DropForeignKey(
                name: "FK_VenueLayouts_VenueBoxes_VenueBoxId",
                table: "VenueLayouts");

            migrationBuilder.DropForeignKey(
                name: "FK_Venues_LookupItems_TypeId",
                table: "Venues");

            migrationBuilder.DropTable(
                name: "SeatAssigns");

            migrationBuilder.DropTable(
                name: "SeatProperties");

            migrationBuilder.DropTable(
                name: "Seatings");

            migrationBuilder.DropTable(
                name: "VenueBoxes");

            migrationBuilder.DropIndex(
                name: "IX_Venues_TypeId",
                table: "Venues");

            migrationBuilder.DropIndex(
                name: "IX_VenueLayoutProps_VenueLayoutId",
                table: "VenueLayoutProps");

            migrationBuilder.DropIndex(
                name: "IX_VenueBlocks_VenueBoxId",
                table: "VenueBlocks");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_VenueId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "VenueLayoutProps");

            migrationBuilder.DropColumn(
                name: "VenueLayoutId",
                table: "VenueLayoutProps");

            migrationBuilder.DropColumn(
                name: "VenueBoxId",
                table: "VenueBlocks");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Sessions");

            migrationBuilder.RenameColumn(
                name: "VenueBoxId",
                table: "VenueLayouts",
                newName: "VenueId");

            migrationBuilder.RenameColumn(
                name: "VenueBlockId",
                table: "VenueLayouts",
                newName: "VenuePropsId");

            migrationBuilder.RenameIndex(
                name: "IX_VenueLayouts_VenueBoxId",
                table: "VenueLayouts",
                newName: "IX_VenueLayouts_VenueId");

            migrationBuilder.RenameIndex(
                name: "IX_VenueLayouts_VenueBlockId",
                table: "VenueLayouts",
                newName: "IX_VenueLayouts_VenuePropsId");

            migrationBuilder.RenameColumn(
                name: "SeatsQuantity",
                table: "VenueLayoutProps",
                newName: "Seats");

            migrationBuilder.RenameColumn(
                name: "RowNames",
                table: "VenueLayoutProps",
                newName: "RowName");

            migrationBuilder.AlterColumn<Guid>(
                name: "TypeId",
                table: "Venues",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "Venues",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldDefaultValueSql: "((0))");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Venues",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(sysutcdatetime())");

            migrationBuilder.AlterColumn<string>(
                name: "Color",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Venues",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<decimal>(
                name: "Y",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "X",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "VenueLayouts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ScaleY",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "ScaleX",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "Rotation",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "OffsetY",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<decimal>(
                name: "OffsetX",
                table: "VenueLayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "VenueLayouts",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldDefaultValueSql: "((0))");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VenueLayouts",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(sysutcdatetime())");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "VenueLayouts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<decimal>(
                name: "StageW",
                table: "VenueLayoutProps",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "StageH",
                table: "VenueLayoutProps",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PitchW",
                table: "VenueLayoutProps",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PitchH",
                table: "VenueLayoutProps",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "VenueLayoutProps",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "VenueLayoutProps",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldDefaultValueSql: "((0))");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VenueLayoutProps",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(sysutcdatetime())");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "VenueLayoutProps",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "VenueLayoutProps",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "VenueBlocks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "VenueBlocks",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true,
                oldDefaultValueSql: "((0))");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VenueBlocks",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(sysutcdatetime())");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "VenueBlocks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "VenueBlocks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "VenueBlocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VenueBlocks_VenueId",
                table: "VenueBlocks",
                column: "VenueId");

            migrationBuilder.AddForeignKey(
                name: "FK_VenueBlocks_Venues_VenueId",
                table: "VenueBlocks",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VenueLayouts_VenueLayoutProps_VenuePropsId",
                table: "VenueLayouts",
                column: "VenuePropsId",
                principalTable: "VenueLayoutProps",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VenueLayouts_Venues_VenueId",
                table: "VenueLayouts",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
