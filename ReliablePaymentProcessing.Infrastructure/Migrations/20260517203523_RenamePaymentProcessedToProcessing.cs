using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReliablePaymentProcessing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenamePaymentProcessedToProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Payments SET Status = 'Processing' WHERE Status = 'Processed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Payments SET Status = 'Processed' WHERE Status = 'Processing'");
        }
    }
}
