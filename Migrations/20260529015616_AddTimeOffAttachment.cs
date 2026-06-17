using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.HRPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeOffAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentName",
                table: "TimeOffRequests",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "TimeOffRequests",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentName",
                table: "TimeOffRequests");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "TimeOffRequests");
        }
    }
}
