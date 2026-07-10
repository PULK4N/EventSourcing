using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    public partial class UniqueConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SerializedEventPayload_AggregateId_OrderNumber",
                table: "SerializedEventPayload",
                columns: new[] { "AggregateId", "OrderNumber" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SerializedEventPayload_AggregateId_OrderNumber",
                table: "SerializedEventPayload");
        }
    }
}
