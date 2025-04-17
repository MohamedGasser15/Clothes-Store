using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clothes_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Add_Columns_To_Db : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Product_Size",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "OrderDetails",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "CartItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Stock_Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Product_Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Stock_Id);
                    table.ForeignKey(
                        name: "FK_Stocks_Products_Product_Id",
                        column: x => x.Product_Id,
                        principalTable: "Products",
                        principalColumn: "Product_Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Product_Id",
                table: "Stocks",
                column: "Product_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "CartItems");

            migrationBuilder.AddColumn<string>(
                name: "Product_Size",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 1,
                column: "Product_Size",
                value: "M");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 2,
                column: "Product_Size",
                value: "L");

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Product_Id",
                keyValue: 3,
                column: "Product_Size",
                value: "S");
        }
    }
}
