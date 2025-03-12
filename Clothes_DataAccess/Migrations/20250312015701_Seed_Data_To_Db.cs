using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Clothes_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Seed_Data_To_Db : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Brands",
                columns: new[] { "Brand_Id", "Brand_Name" },
                values: new object[,]
                {
                    { 1, "Nike" },
                    { 2, "Adidas" },
                    { 3, "Puma" }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Category_Id", "Category_Name" },
                values: new object[,]
                {
                    { 1, "T-Shirts" },
                    { 2, "Pantalon" },
                    { 3, "Shorts" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Product_Id", "Category_Id", "Product_Color", "Product_Description", "Product_Name", "Product_Price", "Product_Rating", "Product_Size", "brand_Id", "imgUrl" },
                values: new object[,]
                {
                    { 1, 1, "Black", "Black T-Shirt Nike", "T-Shirt Nike", 100m, 0, "M", 1, "Nika.jpg" },
                    { 2, 2, "Red", "Red Pantalon Adidas", "Pantalon Adidas", 200m, 0, "L", 2, "Adidas.jpg" },
                    { 3, 3, "Yellow", "Yellow Shorts Puma", "Shorts Puma", 150m, 0, "S", 3, "Puma.jpg" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Brand_Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Brand_Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Brands",
                keyColumn: "Brand_Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Category_Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Category_Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Category_Id",
                keyValue: 3);
        }
    }
}
