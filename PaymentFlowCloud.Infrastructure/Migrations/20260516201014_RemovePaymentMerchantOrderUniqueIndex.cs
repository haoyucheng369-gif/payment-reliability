using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentFlowCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePaymentMerchantOrderUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_MerchantOrderId",
                table: "Payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_MerchantOrderId",
                table: "Payments",
                column: "MerchantOrderId",
                unique: true);
        }
    }
}
