using BusinessAnalytics.Application.Analytics;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Persistence; // adjust namespace to your DbContext
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessAnalytics.Infrastructure.Analytics
{
    public sealed class SalesAnalyticsService : ISalesAnalyticsService
    {
        private readonly AppDbContext _db;

        public SalesAnalyticsService(AppDbContext db)
        {
            _db = db;
        }

        // ----------------------
        //  Summary
        // ----------------------
        public async Task<SalesSummaryDto> GetSummaryAsync(
            int dataSourceId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default)
        {
            var query = FilterFactSales(dataSourceId, from, to);

            // All of this runs in SQL
            var total = await query.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var min = await query.MinAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var max = await query.MaxAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var count = await query.LongCountAsync(ct);
            var avg = count > 0 ? total / count : 0m;

            return new SalesSummaryDto(total, avg, min, max, count);
        }

        // ----------------------
        //  Monthly Trend
        // ----------------------
        public async Task<IReadOnlyList<TimeSeriesPointDto>> GetMonthlyTrendAsync(
            int dataSourceId,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken ct = default)
        {
            var query =
                from fs in FilterFactSales(dataSourceId, startDate, endDate)
                join d in _db.DimDates on fs.DateKey equals d.DateKey
                group fs by new { d.Year, d.Month } into g
                orderby g.Key.Year, g.Key.Month
                select new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Total = g.Sum(x => x.Amount)
                };

            var data = await query.ToListAsync(ct);

            return data
                .Select(x =>
                    new TimeSeriesPointDto(
                        new DateOnly(x.Year, x.Month, 1),
                        x.Total))
                .ToList();
        }

        // ----------------------
        //  Top Products
        // ----------------------
        public async Task<IReadOnlyList<CategoryPointDto>> GetTopProductsAsync(
            int dataSourceId,
            int top,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken ct = default)
        {
            if (top <= 0) top = 10;

            var query =
                from fs in FilterFactSales(dataSourceId, startDate, endDate)
                join p in _db.DimProducts on fs.ProductKey equals p.ProductKey
                group fs by p.ProductName into g
                orderby g.Sum(x => x.Amount) descending
                select new CategoryPointDto(
                    g.Key,
                    g.Sum(x => x.Amount));

            return await query.Take(top).ToListAsync(ct);
        }

        // ----------------------
        //  Top Customers
        // ----------------------
        public async Task<IReadOnlyList<CategoryPointDto>> GetTopCustomersAsync(
            int dataSourceId,
            int top,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken ct = default)
        {
            if (top <= 0) top = 10;

            var query =
                from fs in FilterFactSales(dataSourceId, startDate, endDate)
                join c in _db.DimCustomers on fs.CustomerKey equals c.CustomerKey
                group fs by c.CustomerName into g
                orderby g.Sum(x => x.Amount) descending
                select new CategoryPointDto(
                    g.Key,
                    g.Sum(x => x.Amount));

            return await query.Take(top).ToListAsync(ct);
        }

        // ----------------------
        //  Generic Group-by
        // ----------------------
        public async Task<IReadOnlyList<CategoryPointDto>> GetGroupedAsync(
            int dataSourceId,
            SalesGroupByDimension dimension,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default)
        {
            return dimension switch
            {
                SalesGroupByDimension.Product => await GetTopProductsAsync(dataSourceId, int.MaxValue, from, to, ct),
                SalesGroupByDimension.Customer => await GetTopCustomersAsync(dataSourceId, int.MaxValue, from, to, ct),
                SalesGroupByDimension.Date => await GetByDateAsync(dataSourceId, from, to, ct),
                _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null)
            };
        }

        private async Task<IReadOnlyList<CategoryPointDto>> GetByDateAsync(
            int dataSourceId,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken ct)
        {
            var query =
                from fs in FilterFactSales(dataSourceId, startDate, endDate)
                join d in _db.DimDates on fs.DateKey equals d.DateKey
                group fs by d.Date into g
                orderby g.Key
                select new CategoryPointDto(
                    g.Key.ToString("yyyy-MM-dd"),
                    g.Sum(x => x.Amount));

            return await query.ToListAsync(ct);
        }

        // ----------------------
        //  Shared filter
        // ----------------------
        private IQueryable<FactSales> FilterFactSales(
            int dataSourceId,
            DateOnly? startDate,
            DateOnly? endDate)
        {
            var q = _db.FactSales
                .AsNoTracking()
                .Where(x => x.DataSourceId == dataSourceId);

            if (startDate.HasValue || endDate.HasValue)
            {
                // Join with dates inside the query to keep it SQL-side
                q =
                    from fs in q
                    join d in _db.DimDates on fs.DateKey equals d.DateKey
                    where (!startDate.HasValue || d.Date >= startDate.Value) 
                       && (!endDate.HasValue || d.Date <= endDate.Value)
                    select fs;
            }

            return q;
        }
    }
}

