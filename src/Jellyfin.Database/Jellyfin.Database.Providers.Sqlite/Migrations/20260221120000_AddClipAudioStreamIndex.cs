using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Server.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddClipAudioStreamIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioStreamIndex",
                table: "Clips",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioStreamIndex",
                table: "Clips");
        }
    }
}
