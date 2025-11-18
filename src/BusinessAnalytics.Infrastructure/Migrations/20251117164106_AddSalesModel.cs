using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DimCustomers",
                columns: table => new
                {
                    CustomerKey = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimCustomers", x => x.CustomerKey);
                });

            migrationBuilder.CreateTable(
                name: "DimProducts",
                columns: table => new
                {
                    ProductKey = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubCategory = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimProducts", x => x.ProductKey);
                });

            migrationBuilder.CreateTable(
                name: "FactSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DateKey = table.Column<int>(type: "int", nullable: false),
                    ProductKey = table.Column<int>(type: "int", nullable: false),
                    CustomerKey = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DimCustomerCustomerKey = table.Column<int>(type: "int", nullable: true),
                    DimProductProductKey = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactSales_DimCustomers_CustomerKey",
                        column: x => x.CustomerKey,
                        principalTable: "DimCustomers",
                        principalColumn: "CustomerKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactSales_DimCustomers_DimCustomerCustomerKey",
                        column: x => x.DimCustomerCustomerKey,
                        principalTable: "DimCustomers",
                        principalColumn: "CustomerKey");
                    table.ForeignKey(
                        name: "FK_FactSales_DimDates_DateKey",
                        column: x => x.DateKey,
                        principalTable: "DimDates",
                        principalColumn: "DateKey",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FactSales_DimProducts_DimProductProductKey",
                        column: x => x.DimProductProductKey,
                        principalTable: "DimProducts",
                        principalColumn: "ProductKey");
                    table.ForeignKey(
                        name: "FK_FactSales_DimProducts_ProductKey",
                        column: x => x.ProductKey,
                        principalTable: "DimProducts",
                        principalColumn: "ProductKey",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FactSales_CustomerKey",
                table: "FactSales",
                column: "CustomerKey");

            migrationBuilder.CreateIndex(
                name: "IX_FactSales_DateKey",
                table: "FactSales",
                column: "DateKey");

            migrationBuilder.CreateIndex(
                name: "IX_FactSales_DimCustomerCustomerKey",
                table: "FactSales",
                column: "DimCustomerCustomerKey");

            migrationBuilder.CreateIndex(
                name: "IX_FactSales_DimProductProductKey",
                table: "FactSales",
                column: "DimProductProductKey");

            migrationBuilder.CreateIndex(
                name: "IX_FactSales_ProductKey",
                table: "FactSales",
                column: "ProductKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FactSales");

            migrationBuilder.DropTable(
                name: "DimCustomers");

            migrationBuilder.DropTable(
                name: "DimProducts");
        }
    }
}
