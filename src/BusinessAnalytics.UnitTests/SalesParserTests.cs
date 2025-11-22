using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Parsing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BusinessAnalytics.UnitTests;

public class SalesParserTests : IClassFixture<SqliteAppDbFixture>
{
    private readonly SqliteAppDbFixture _fx;

    public SalesParserTests(SqliteAppDbFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task ParseAndImportAsync_ValidCsvWithMapping_InsertsOneFactRow()
    {
        // ---------------- Arrange ----------------
        var db = _fx.Db;
        // 0) Seed DimDate entry for 2024-02-01 (the date used by the CSV)
        var date = new DateOnly(2024, 2, 1);
        var dateKey = DimDate.ToDateKey(date);

        // calculate ISO week
        var isoWeek = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

        // Greek month name
        var culture = CultureInfo.GetCultureInfo("el-GR");
        var monthName = date.ToDateTime(TimeOnly.MinValue)
                            .ToString("MMMM", culture);

        // quarter: 1 = Jan-Mar, 2 = Apr-Jun, etc.
        var quarter = (date.Month - 1) / 3 + 1;

        db.DimDates.Add(new DimDate
        {
            DateKey = dateKey,
            Date = date,
            Year = date.Year,
            Quarter = quarter,
            Month = date.Month,
            MonthName = monthName,
            Day = date.Day,
            IsoWeek = isoWeek
        });

        await db.SaveChangesAsync();

        // 1) Seed a fake owner user to satisfy FK
        var user = new ApplicationUser
        {
            UserName = "test-user@example.com",
            Email = "test-user@example.com",
            DisplayName = "Test User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 2) DataSource
        var ds = new DataSource
        {
            Name = "Test Sales",
            Type = DataSourceType.Sales,   // ✅ enum, not string
            OwnerId = user.Id
        };
        db.DataSources.Add(ds);
        await db.SaveChangesAsync();

        // 3) Mapping: canonical -> Greek headers (these MUST match the CSV header exactly)
        var columnMap = new Dictionary<string, string>
        {
            ["Date"] = "Ημερομηνία",
            ["Product"] = "Προϊόν",
            ["Customer"] = "Πελάτης",
            ["Quantity"] = "Ποσότητα",
            ["Amount"] = "Ποσό"
        };

        var mapping = new DataSourceMapping
        {
            DataSourceId = ds.Id,
            Kind = "Sales",
            Culture = "el-GR",
            SheetName = "",
            ColumnMapJson = JsonSerializer.Serialize(columnMap)
        };
        db.DataSourceMappings.Add(mapping);
        await db.SaveChangesAsync();

        // 4) Temp CSV file (semicolon delimiter, Greek culture format for decimal)
        var tmpDir = Path.Combine(Path.GetTempPath(), "ba-tests");
        Directory.CreateDirectory(tmpDir);
        var csvPath = Path.Combine(tmpDir, $"sales_{Guid.NewGuid():N}.csv");

        var csv =
            "Ημερομηνία;Προϊόν;Πελάτης;Ποσότητα;Ποσό\r\n" +
            "01/02/2024;Προϊόν Α;Πελάτης Α;2;1234,56\r\n";

        await File.WriteAllTextAsync(csvPath, csv);

        // 5) RawImport pointing to that file
        var import = new RawImport
        {
            DataSourceId = ds.Id,
            OriginalFilePath = csvPath,
            Status = ImportStatus.Staged,
            Rows = 0,
            StartedAtUtc = DateTime.UtcNow
        };
        db.RawImports.Add(import);
        await db.SaveChangesAsync();

        var sut = new SalesParser(db);

        // ---------------- Act ----------------
        var result = await sut.ParseAndImportAsync(import.Id, CancellationToken.None);

        // Reload entities from DB
        await db.Entry(import).ReloadAsync();
        var factRows = await db.FactSales.ToListAsync();

        // ---------------- Assert ----------------
        result.Success.Should().BeTrue(result.Error);
        result.Rows.Should().Be(1);

        import.Status.Should().Be(ImportStatus.Parsed);
        import.Rows.Should().Be(1);
        import.ErrorMessage.Should().BeNull();

        factRows.Should().HaveCount(1);
        var fs = factRows[0];

        fs.DataSourceId.Should().Be(ds.Id);
        fs.Quantity.Should().Be(2);

        // Amount parsing: SalesParser tries invariant, then falls back to culture ("el-GR"),
        // so "1234,56" should become 1234.56m
        fs.Amount.Should().Be(1234.56m);

        // DateKey corresponds to 2024-02-01
        fs.DateKey.Should().Be(DimDate.ToDateKey(new DateOnly(2024, 2, 1)));

        var product = await db.DimProducts.FindAsync(fs.ProductKey);
        var customer = await db.DimCustomers.FindAsync(fs.CustomerKey);

        product!.ProductName.Should().Be("Προϊόν Α");
        customer!.CustomerName.Should().Be("Πελάτης Α");
    }
}
