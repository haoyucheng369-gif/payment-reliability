using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentFlowCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationIdAndMerchantOrderIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TraceId",
                table: "Payments",
                newName: "CorrelationId");

            migrationBuilder.AlterColumn<string>(
                name: "MerchantOrderId",
                table: "Payments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "Payments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MerchantOrderId",
                table: "Payments",
                column: "MerchantOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_MerchantOrderId",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "MerchantOrderId",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.RenameColumn(
                name: "CorrelationId",
                table: "Payments",
                newName: "TraceId");
        }
    }
}
