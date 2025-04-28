using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clothes_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Add_DataAdded_Column_in_Product : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
      name: "DateAdded",
      table: "Products",
      type: "datetime2",
      nullable: false,
      defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "NewArrivalDurationDays",
                table: "Products",
                type: "int",
                nullable: true);

            // Update seed data
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 1,
                columns: new[] { "DateAdded", "NewArrivalDurationDays" },
                values: new object[] { new DateTime(2025, 4, 28, 22, 9, 8, 320, DateTimeKind.Local).AddTicks(8998), null });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 2,
                columns: new[] { "DateAdded", "NewArrivalDurationDays" },
                values: new object[] { new DateTime(2025, 4, 28, 22, 9, 8, 320, DateTimeKind.Local).AddTicks(9064), null });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 3,
                columns: new[] { "DateAdded", "NewArrivalDurationDays" },
                values: new object[] { new DateTime(2025, 4, 28, 22, 9, 8, 320, DateTimeKind.Local).AddTicks(9067), null });

            // Add this SQL command to fix legacy dates
            migrationBuilder.Sql(
                @"UPDATE Products 
          SET DateAdded = DATEADD(day, -60, GETUTCDATE()) 
          WHERE DateAdded = '0001-01-01'
            AND Product_Id NOT IN (1, 2, 3)"); // Exclude seed data
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NewArrivalDurationDays",
                table: "Products");
        }
    }
}
