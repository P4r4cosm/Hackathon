using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundService.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAudioRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploadUserId",
                table: "AudioRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploadUserId",
                table: "AudioRecords",
                type: "text",
                nullable: true);
        }
    }
}
