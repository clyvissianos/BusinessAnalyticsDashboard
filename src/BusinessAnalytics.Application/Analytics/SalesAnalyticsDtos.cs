using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Application.Analytics
{
    public sealed record TimeSeriesPointDto(
        DateOnly Date,
        decimal Value);

    public sealed record CategoryPointDto(
        string Category,
        decimal Value);

    public sealed record SalesSummaryDto(
        decimal Total,
        decimal Average,
        decimal Minimum,
        decimal Maximum,
        long Count);

    public enum SalesGroupByDimension
    {
        Product = 0,
        Customer = 1,
        Date = 2
    }
}
