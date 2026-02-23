using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestingDemo.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedDepartmentUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedCustomerCareId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedDocumentOfficerId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedFinanceId",
                table: "Clients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedCustomerCareId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AssignedDocumentOfficerId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "AssignedFinanceId",
                table: "Clients");
        }
    }
}
