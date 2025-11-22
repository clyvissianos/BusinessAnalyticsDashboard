using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessAnalytics.Application.Analytics
{
    public interface ISalesAnalyticsService
    {
        Task<SalesSummaryDto> GetSummaryAsync(
            int dataSourceId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default);

        Task<IReadOnlyList<TimeSeriesPointDto>> GetMonthlyTrendAsync(
            int dataSourceId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default);

        Task<IReadOnlyList<CategoryPointDto>> GetTopProductsAsync(
            int dataSourceId,
            int top,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default);

        Task<IReadOnlyList<CategoryPointDto>> GetTopCustomersAsync(
            int dataSourceId,
            int top,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default);

        Task<IReadOnlyList<CategoryPointDto>> GetGroupedAsync(
            int dataSourceId,
            SalesGroupByDimension dimension,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct = default);

        Task<SalesDashboardDto> GetDashboardAsync(
            int dataSourceId,
            DateOnly? from,
            DateOnly? to,
            int topProducts = 5,
            int topCustomers = 5,
            CancellationToken ct = default);
    }
}

