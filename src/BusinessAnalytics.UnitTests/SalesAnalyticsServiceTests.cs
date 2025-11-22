using BusinessAnalytics.Application.Analytics;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Analytics;
using BusinessAnalytics.Infrastructure.Persistence;
using FluentAssertions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace BusinessAnalytics.UnitTests
{
    // Uses the same shared in-memory SQLite DB as SalesParserTests
    public class SalesAnalyticsServiceTests : IClassFixture<SqliteAppDbFixture>
    {
        private readonly AppDbContext _db;
        private readonly SalesAnalyticsService _svc;

        public SalesAnalyticsServiceTests(SqliteAppDbFixture fx)
        {
            _db = fx.Db;
            _svc = new SalesAnalyticsService(_db);
        }

        // ---------- helpers ----------

        private DataSource CreateDataSource(string name)
        {
            // ensure owner exists
            var owner = _db.Users.FirstOrDefault(u => u.UserName == "test-owner@example.com");
            if (owner == null)
            {
                owner = new ApplicationUser
                {
                    UserName = "test-owner@example.com",
                    Email = "test-owner@example.com",
                    DisplayName = "Test Owner"
                };

                _db.Users.Add(owner);
                _db.SaveChanges();
            }

            var ds = new DataSource
            {
                Name = name,
                Type = DataSourceType.Sales,
                OwnerId = owner.Id   // ✔ correct FK
            };

            _db.DataSources.Add(ds);
            _db.SaveChanges();

            return ds;
        }


        private DimDate EnsureDimDate(DateOnly date)
        {
            var key = DimDate.ToDateKey(date);
            var existing = _db.DimDates.SingleOrDefault(d => d.DateKey == key);
            if (existing != null) return existing;

            var isoWeek = System.Globalization.ISOWeek
                .GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

            var dd = new DimDate
            {
                DateKey = key,
                Date = date,
                Year = date.Year,
                Quarter = (date.Month - 1) / 3 + 1,
                Month = date.Month,
                MonthName = date.ToString("MMMM"),
                Day = date.Day,
                IsoWeek = isoWeek
            };

            _db.DimDates.Add(dd);
            _db.SaveChanges();
            return dd;
        }

        private void AddSale(
            DataSource ds,
            DateOnly date,
            string productName,
            string customerName,
            decimal amount)
        {
            var dimDate = EnsureDimDate(date);

            var product = new DimProduct { ProductName = productName };
            var customer = new DimCustomer { CustomerName = customerName };

            _db.DimProducts.Add(product);
            _db.DimCustomers.Add(customer);
            _db.SaveChanges();

            var fs = new FactSales
            {
                DataSourceId = ds.Id,
                DateKey = dimDate.DateKey,
                ProductKey = product.ProductKey,
                CustomerKey = customer.CustomerKey,
                Amount = amount
            };

            _db.FactSales.Add(fs);
            _db.SaveChanges();
        }

        // ---------- tests ----------

        [Fact]
        public async Task GetSummaryAsync_ComputesAggregates_ForSingleDataSource()
        {
            // arrange
            var ds = CreateDataSource("DS-Summary");

            AddSale(ds, new DateOnly(2024, 1, 10), "P1", "C1", 100m);
            AddSale(ds, new DateOnly(2024, 1, 15), "P2", "C2", 200m);
            AddSale(ds, new DateOnly(2024, 2, 1), "P3", "C3", 300m);

            // noise – must be ignored
            var other = CreateDataSource("Other");
            AddSale(other, new DateOnly(2024, 1, 1), "X", "Y", 9999m);

            // act
            var summary = await _svc.GetSummaryAsync(ds.Id, null, null);

            // assert  (adjust property names if different in your DTO)
            summary.Total.Should().Be(600m);
            summary.Count.Should().Be(3);
            summary.Minimum.Should().Be(100m);
            summary.Maximum.Should().Be(300m);
            summary.Average.Should().Be(200m);
        }

        [Fact]
        public async Task GetMonthlyTrendAsync_GroupsByYearAndMonth()
        {
            // arrange
            var ds = CreateDataSource("DS-Monthly");

            AddSale(ds, new DateOnly(2024, 1, 10), "P1", "C1", 100m);
            AddSale(ds, new DateOnly(2024, 1, 20), "P2", "C2", 50m);
            AddSale(ds, new DateOnly(2024, 2, 5), "P3", "C3", 200m);

            // act
            var trend = await _svc.GetMonthlyTrendAsync(ds.Id, null, null);

            // assert (assuming TimeSeriesPointDto has Date, Value)
            trend.Should().HaveCount(2);
            trend[0].Date.Should().Be(new DateOnly(2024, 1, 1));
            trend[0].Value.Should().Be(150m);
            trend[1].Date.Should().Be(new DateOnly(2024, 2, 1));
            trend[1].Value.Should().Be(200m);
        }

        [Fact]
        public async Task GetTopProductsAsync_OrdersByAmount_AndHonorsTopParameter()
        {
            // arrange
            var ds = CreateDataSource("DS-TopProducts");

            AddSale(ds, new DateOnly(2024, 1, 1), "Cola", "C1", 300m);
            AddSale(ds, new DateOnly(2024, 1, 2), "Water", "C2", 100m);
            AddSale(ds, new DateOnly(2024, 1, 3), "Snacks", "C3", 200m);

            // act
            var top2 = await _svc.GetTopProductsAsync(ds.Id, 2, null, null);

            // assert (assuming CategoryPointDto has Category, Value)
            top2.Should().HaveCount(2);
            top2[0].Category.Should().Be("Cola");
            top2[0].Value.Should().Be(300m);
            top2[1].Category.Should().Be("Snacks");
            top2[1].Value.Should().Be(200m);
        }

        [Fact]
        public async Task GetGroupedAsync_WithDateDimension_UsesDailyBuckets()
        {
            // arrange
            var ds = CreateDataSource("DS-GroupByDate");

            var d1 = new DateOnly(2024, 1, 10);
            var d2 = new DateOnly(2024, 1, 11);

            AddSale(ds, d1, "P1", "C1", 100m);
            AddSale(ds, d1, "P2", "C2", 50m);
            AddSale(ds, d2, "P3", "C3", 200m);

            // act
            var grouped = await _svc.GetGroupedAsync(
                ds.Id,
                SalesGroupByDimension.Date,
                null,
                null);

            // assert – GetByDateAsync returns CategoryPointDto with date string "yyyy-MM-dd"
            grouped.Should().HaveCount(2);
            grouped[0].Category.Should().Be(d1.ToString("yyyy-MM-dd"));
            grouped[0].Value.Should().Be(150m);
            grouped[1].Category.Should().Be(d2.ToString("yyyy-MM-dd"));
            grouped[1].Value.Should().Be(200m);
        }
    }
}

