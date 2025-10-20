using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestingDemo.Migrations
{
    /// <inheritdoc />
    public partial class MultiPhotoSupportForRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProofOfCompletionPhoto",
                table: "PermitRequirements");

            migrationBuilder.CreateTable(
                name: "RequirementPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequirementId = table.Column<int>(type: "int", nullable: false),
                    PhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementPhotos_PermitRequirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "PermitRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementPhotos_RequirementId",
                table: "RequirementPhotos",
                column: "RequirementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementPhotos");

            migrationBuilder.AddColumn<string>(
                name: "ProofOfCompletionPhoto",
                table: "PermitRequirements",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
