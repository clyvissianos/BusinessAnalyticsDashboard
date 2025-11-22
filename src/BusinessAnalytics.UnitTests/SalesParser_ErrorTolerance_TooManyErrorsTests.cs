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

public class SalesParser_ErrorTolerance_TooManyErrorsTests : IClassFixture<SqliteAppDbFixture>
{
    private readonly SqliteAppDbFixture _fx;

    public SalesParser_ErrorTolerance_TooManyErrorsTests(SqliteAppDbFixture fx)
    {
        _fx = fx;
    }

    [Fact]
    public async Task ParseAndImportAsync_FailsWhenMoreThanFivePercentRowsHaveInvalidAmounts()
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
            UserName = "tolerance-fail-user@example.com",
            Email = "tolerance-fail-user@example.com",
            DisplayName = "Tolerance Fail User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // ---------- DataSource ----------
        var ds = new DataSource
        {
            Name = "Tolerance Fail DS",
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

        // ---------- CSV: 20 rows, 2 invalid amounts (10%) ----------
        var tmpDir = Path.Combine(Path.GetTempPath(), "ba-tests");
        Directory.CreateDirectory(tmpDir);
        var csvPath = Path.Combine(tmpDir, $"tolerance_fail_{Guid.NewGuid():N}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Ημερομηνία;Προϊόν;Πελάτης;Ποσότητα;Ποσό");

        // 18 valid rows
        for (int i = 1; i <= 18; i++)
        {
            sb.AppendLine($"01/02/2024;Προϊόν {i};Πελάτης {i};1;100,00");
        }

        // 2 invalid amount rows
        sb.AppendLine("01/02/2024;Προϊόν Χ1;Πελάτης Χ1;1;ABC");
        sb.AppendLine("01/02/2024;Προϊόν Χ2;Πελάτης Χ2;1;ABC");

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
        // 2 invalid rows out of 20 => 10% > 5% → parser should mark import as failed.
        result.Success.Should().BeFalse();
        import.Status.Should().Be(ImportStatus.Failed);
        import.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        import.ErrorMessage!.Should().Contain("Error rate");

        // Successful rows may still be persisted; we don't require rollback.
        facts.Count.Should().BeGreaterThan(0);
        facts.Count.Should().Be(18); // if you want strict behaviour
    }
}

