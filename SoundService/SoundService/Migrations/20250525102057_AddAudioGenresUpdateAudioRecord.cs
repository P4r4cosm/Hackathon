using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundService.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioGenresUpdateAudioRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioRecords_Genres_GenreId",
                table: "AudioRecords");

            migrationBuilder.AlterColumn<int>(
                name: "GenreId",
                table: "AudioRecords",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "AudioGenres",
                columns: table => new
                {
                    AudioRecordId = table.Column<int>(type: "integer", nullable: false),
                    GenreId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioGenres", x => new { x.AudioRecordId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_AudioGenres_AudioRecords_AudioRecordId",
                        column: x => x.AudioRecordId,
                        principalTable: "AudioRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AudioGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioGenres_GenreId",
                table: "AudioGenres",
                column: "GenreId");

            migrationBuilder.AddForeignKey(
                name: "FK_AudioRecords_Genres_GenreId",
                table: "AudioRecords",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioRecords_Genres_GenreId",
                table: "AudioRecords");

            migrationBuilder.DropTable(
                name: "AudioGenres");

            migrationBuilder.AlterColumn<int>(
                name: "GenreId",
                table: "AudioRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioRecords_Genres_GenreId",
                table: "AudioRecords",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
