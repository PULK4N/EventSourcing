using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    public partial class SerializedEventInfoAndEventData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SerializedPayloadMessageData",
                table: "SerializedPayloadMessage",
                newName: "SerializedEventExecutionInfo");

            migrationBuilder.AddColumn<string>(
                name: "SerializedEventData",
                table: "SerializedPayloadMessage",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedEventData",
                table: "SerializedPayloadMessage");

            migrationBuilder.RenameColumn(
                name: "SerializedEventExecutionInfo",
                table: "SerializedPayloadMessage",
                newName: "SerializedPayloadMessageData");
        }
    }
}
