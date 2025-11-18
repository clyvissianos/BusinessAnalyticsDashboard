using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceToFactSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DataSourceId",
                table: "FactSales",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FactSales_DataSourceId",
                table: "FactSales",
                column: "DataSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_FactSales_DataSources_DataSourceId",
                table: "FactSales",
                column: "DataSourceId",
                principalTable: "DataSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FactSales_DataSources_DataSourceId",
                table: "FactSales");

            migrationBuilder.DropIndex(
                name: "IX_FactSales_DataSourceId",
                table: "FactSales");

            migrationBuilder.DropColumn(
                name: "DataSourceId",
                table: "FactSales");
        }
    }
}
