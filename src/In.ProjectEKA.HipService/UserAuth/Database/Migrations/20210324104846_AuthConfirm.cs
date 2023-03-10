using Microsoft.EntityFrameworkCore.Migrations;

namespace In.ProjectEKA.HipService.Migrations
{
    public partial class AuthConfirm : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthConfirm",
                columns: table => new
                {
                    HealthId = table.Column<string>(nullable: false),
                    AccessToken = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthConfirm", x => x.HealthId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthConfirm");
        }
    }
}
