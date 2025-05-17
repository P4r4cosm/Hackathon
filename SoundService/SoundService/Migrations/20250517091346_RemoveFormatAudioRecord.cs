using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFormatAudioRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Format",
                table: "AudioRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "AudioRecords",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
