using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clothes_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Add_Column_To_ApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "AspNetUsers");
        }
    }
}
