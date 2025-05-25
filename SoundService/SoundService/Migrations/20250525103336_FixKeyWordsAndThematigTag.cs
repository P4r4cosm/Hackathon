using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundService.Migrations
{
    /// <inheritdoc />
    public partial class FixKeyWordsAndThematigTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioKeyword_AudioRecords_AudioRecordId",
                table: "AudioKeyword");

            migrationBuilder.DropForeignKey(
                name: "FK_AudioKeyword_Keyword_KeywordId",
                table: "AudioKeyword");

            migrationBuilder.DropForeignKey(
                name: "FK_AudioThematicTag_AudioRecords_AudioRecordId",
                table: "AudioThematicTag");

            migrationBuilder.DropForeignKey(
                name: "FK_AudioThematicTag_ThematicTag_ThematicTagId",
                table: "AudioThematicTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ThematicTag",
                table: "ThematicTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Keyword",
                table: "Keyword");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AudioThematicTag",
                table: "AudioThematicTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AudioKeyword",
                table: "AudioKeyword");

            migrationBuilder.RenameTable(
                name: "ThematicTag",
                newName: "ThematicTags");

            migrationBuilder.RenameTable(
                name: "Keyword",
                newName: "Keywords");

            migrationBuilder.RenameTable(
                name: "AudioThematicTag",
                newName: "AudioThematicTags");

            migrationBuilder.RenameTable(
                name: "AudioKeyword",
                newName: "AudioKeywords");

            migrationBuilder.RenameIndex(
                name: "IX_AudioThematicTag_ThematicTagId",
                table: "AudioThematicTags",
                newName: "IX_AudioThematicTags_ThematicTagId");

            migrationBuilder.RenameIndex(
                name: "IX_AudioKeyword_KeywordId",
                table: "AudioKeywords",
                newName: "IX_AudioKeywords_KeywordId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ThematicTags",
                table: "ThematicTags",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Keywords",
                table: "Keywords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AudioThematicTags",
                table: "AudioThematicTags",
                columns: new[] { "AudioRecordId", "ThematicTagId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AudioKeywords",
                table: "AudioKeywords",
                columns: new[] { "AudioRecordId", "KeywordId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AudioKeywords_AudioRecords_AudioRecordId",
                table: "AudioKeywords",
                column: "AudioRecordId",
                principalTable: "AudioRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioKeywords_Keywords_KeywordId",
                table: "AudioKeywords",
                column: "KeywordId",
                principalTable: "Keywords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioThematicTags_AudioRecords_AudioRecordId",
                table: "AudioThematicTags",
                column: "AudioRecordId",
                principalTable: "AudioRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioThematicTags_ThematicTags_ThematicTagId",
                table: "AudioThematicTags",
                column: "ThematicTagId",
                principalTable: "ThematicTags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioKeywords_AudioRecords_AudioRecordId",
                table: "AudioKeywords");

            migrationBuilder.DropForeignKey(
                name: "FK_AudioKeywords_Keywords_KeywordId",
                table: "AudioKeywords");

            migrationBuilder.DropForeignKey(
                name: "FK_AudioThematicTags_AudioRecords_AudioRecordId",
                table: "AudioThematicTags");

            migrationBuilder.DropForeignKey(
                name: "FK_AudioThematicTags_ThematicTags_ThematicTagId",
                table: "AudioThematicTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ThematicTags",
                table: "ThematicTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Keywords",
                table: "Keywords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AudioThematicTags",
                table: "AudioThematicTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AudioKeywords",
                table: "AudioKeywords");

            migrationBuilder.RenameTable(
                name: "ThematicTags",
                newName: "ThematicTag");

            migrationBuilder.RenameTable(
                name: "Keywords",
                newName: "Keyword");

            migrationBuilder.RenameTable(
                name: "AudioThematicTags",
                newName: "AudioThematicTag");

            migrationBuilder.RenameTable(
                name: "AudioKeywords",
                newName: "AudioKeyword");

            migrationBuilder.RenameIndex(
                name: "IX_AudioThematicTags_ThematicTagId",
                table: "AudioThematicTag",
                newName: "IX_AudioThematicTag_ThematicTagId");

            migrationBuilder.RenameIndex(
                name: "IX_AudioKeywords_KeywordId",
                table: "AudioKeyword",
                newName: "IX_AudioKeyword_KeywordId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ThematicTag",
                table: "ThematicTag",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Keyword",
                table: "Keyword",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AudioThematicTag",
                table: "AudioThematicTag",
                columns: new[] { "AudioRecordId", "ThematicTagId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AudioKeyword",
                table: "AudioKeyword",
                columns: new[] { "AudioRecordId", "KeywordId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AudioKeyword_AudioRecords_AudioRecordId",
                table: "AudioKeyword",
                column: "AudioRecordId",
                principalTable: "AudioRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioKeyword_Keyword_KeywordId",
                table: "AudioKeyword",
                column: "KeywordId",
                principalTable: "Keyword",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioThematicTag_AudioRecords_AudioRecordId",
                table: "AudioThematicTag",
                column: "AudioRecordId",
                principalTable: "AudioRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioThematicTag_ThematicTag_ThematicTagId",
                table: "AudioThematicTag",
                column: "ThematicTagId",
                principalTable: "ThematicTag",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
