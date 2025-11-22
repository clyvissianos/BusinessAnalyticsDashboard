using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Parsing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BusinessAnalytics.UnitTests;

public class SalesParser_ErrorTolerance_AllowedTests : IClassFixture<SqliteAppDbFixture>
{
    private readonly SqliteAppDbFixture _fx;

    public SalesParser_ErrorTolerance_AllowedTests(SqliteAppDbFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task ParseAndImportAsync_AllowsUpToFivePercentInvalidAmountRows()
    {
        var db = _fx.Db;

        // ---------- Seed DimDate for 2024-02-01 ----------
        var date = new DateOnly(2024, 2, 1);
        var dateKey = DimDate.ToDateKey(date);
        var isoWeek = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        var culture = CultureInfo.GetCultureInfo("el-GR");
        var monthName = date.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture);
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

        // ---------- Seed owner user ----------
        var user = new ApplicationUser
        {
            UserName = "tolerance-user@example.com",
            Email = "tolerance-user@example.com",
            DisplayName = "Tolerance User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // ---------- DataSource ----------
        var ds = new DataSource
        {
            Name = "Tolerance Test DS",
            Type = DataSourceType.Sales,
            OwnerId = user.Id
        };
        db.DataSources.Add(ds);
        await db.SaveChangesAsync();

        // ---------- Mapping ----------
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

        // ---------- CSV: 20 rows, 1 invalid amount (5%) ----------
        var tmpDir = Path.Combine(Path.GetTempPath(), "ba-tests");
        Directory.CreateDirectory(tmpDir);
        var csvPath = Path.Combine(tmpDir, $"tolerance_ok_{Guid.NewGuid():N}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Ημερομηνία;Προϊόν;Πελάτης;Ποσότητα;Ποσό");

        // 19 valid rows
        for (int i = 1; i <= 19; i++)
        {
            sb.AppendLine($"01/02/2024;Προϊόν {i};Πελάτης {i};1;100,00");
        }

        // 1 invalid row (bad amount)
        sb.AppendLine("01/02/2024;Προϊόν Χ;Πελάτης Χ;1;ABC");

        await File.WriteAllTextAsync(csvPath, sb.ToString(), Encoding.UTF8);

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

        // ---------- Act ----------
        var result = await sut.ParseAndImportAsync(import.Id, CancellationToken.None);

        await db.Entry(import).ReloadAsync();
        var facts = await db.FactSales.Where(f => f.DataSourceId == ds.Id).ToListAsync();

        // ---------- Assert ----------
        // With correct error-rate calculation based on total rows (20),
        // 1 invalid row -> 5% -> should still succeed.
        result.Success.Should().BeTrue(result.Error);
        import.Status.Should().Be(ImportStatus.Parsed);

        // 19 valid fact rows expected
        facts.Should().HaveCount(19);
    }
}

