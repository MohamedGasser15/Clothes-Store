using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clothes_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Add_Data : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 1,
                column: "DateAdded",
                value: new DateTime(2025, 4, 28, 22, 41, 24, 761, DateTimeKind.Local).AddTicks(8428));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 2,
                column: "DateAdded",
                value: new DateTime(2025, 4, 28, 22, 41, 24, 761, DateTimeKind.Local).AddTicks(8497));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 3,
                column: "DateAdded",
                value: new DateTime(2025, 4, 28, 22, 41, 24, 761, DateTimeKind.Local).AddTicks(8499));
            migrationBuilder.Sql(
      @"UPDATE Products 
          SET DateAdded = DATEADD(day, -60, GETUTCDATE()) 
          WHERE DateAdded = '0001-01-01'
            AND Product_Id NOT IN (1, 2, 3)"); // Exclude seed data
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 1,
                column: "DateAdded",
                value: new DateTime(2025, 4, 28, 22, 9, 8, 320, DateTimeKind.Local).AddTicks(8998));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 2,
                column: "DateAdded",
                value: new DateTime(2025, 4, 28, 22, 9, 8, 320, DateTimeKind.Local).AddTicks(9064));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 3,
                column: "DateAdded",
                value: new DateTime(2025, 4, 28, 22, 9, 8, 320, DateTimeKind.Local).AddTicks(9067));
        }
    }
}
