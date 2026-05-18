using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentFlowCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalStatusIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_CreatedAt",
                table: "Payments",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_Status_CreatedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_CreatedAt",
                table: "Orders");
        }
    }
}
