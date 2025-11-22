using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Application.Analytics;

public sealed record SalesDashboardDto(
    SalesSummaryDto Summary,
    IReadOnlyList<TimeSeriesPointDto> MonthlyTrend,
    IReadOnlyList<CategoryPointDto> TopProducts,
    IReadOnlyList<CategoryPointDto> TopCustomers);

