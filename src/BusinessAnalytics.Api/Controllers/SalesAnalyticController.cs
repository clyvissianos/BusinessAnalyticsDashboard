using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BusinessAnalytics.Application.Analytics;
using BusinessAnalytics.Infrastructure.Persistence; // your DbContext
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusinessAnalytics.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/analytics/sales")]
    [Authorize(Roles = "Analyst,Admin,Viewer")]
    public sealed class SalesAnalyticsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ISalesAnalyticsService _svc;

        public SalesAnalyticsController(AppDbContext db, ISalesAnalyticsService svc)
        {
            _db = db;
            _svc = svc;
        }

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User id not found in token.");

        // Helper: ensures DS belongs to current user; throws 404 if not
        private async Task<bool> EnsureDataSourceOwnedAsync(int dataSourceId, CancellationToken ct)
        {
            var uid = CurrentUserId;
            var exists = await _db.DataSources
                .AnyAsync(x => x.Id == dataSourceId && x.OwnerId == uid, ct);
            return exists;
        }

        // GET api/v1/analytics/sales/{dataSourceId}/summary?from=2024-01-01&to=2024-12-31
        [HttpGet("{dataSourceId:int}/summary")]
        public async Task<IActionResult> GetSummary(
            int dataSourceId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken ct)
        {
            if (!await EnsureDataSourceOwnedAsync(dataSourceId, ct))
                return NotFound(new { error = "DataSource not found" });

            var result = await _svc.GetSummaryAsync(dataSourceId, from, to, ct);
            return Ok(result);
        }

        // GET api/v1/analytics/sales/{dataSourceId}/monthly?from=...&to=...
        [HttpGet("{dataSourceId:int}/monthly")]
        public async Task<IActionResult> GetMonthly(
            int dataSourceId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken ct)
        {
            if (!await EnsureDataSourceOwnedAsync(dataSourceId, ct))
                return NotFound(new { error = "DataSource not found" });

            var result = await _svc.GetMonthlyTrendAsync(dataSourceId, from, to, ct);
            return Ok(result);
        }

        // GET api/v1/analytics/sales/{dataSourceId}/top-products?top=10&from=...&to=...
        [HttpGet("{dataSourceId:int}/top-products")]
        public async Task<IActionResult> GetTopProducts(
            int dataSourceId,
            [FromQuery] int top = 10,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null,
            CancellationToken ct = default)
        {
            if (!await EnsureDataSourceOwnedAsync(dataSourceId, ct))
                return NotFound(new { error = "DataSource not found" });

            var result = await _svc.GetTopProductsAsync(dataSourceId, top, from, to, ct);
            return Ok(result);
        }

        // GET api/v1/analytics/sales/{dataSourceId}/top-customers?top=10&from=...&to=...
        [HttpGet("{dataSourceId:int}/top-customers")]
        public async Task<IActionResult> GetTopCustomers(
            int dataSourceId,
            [FromQuery] int top = 10,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null,
            CancellationToken ct = default)
        {
            if (!await EnsureDataSourceOwnedAsync(dataSourceId, ct))
                return NotFound(new { error = "DataSource not found" });

            var result = await _svc.GetTopCustomersAsync(dataSourceId, top, from, to, ct);
            return Ok(result);
        }

        // GET api/v1/analytics/sales/{dataSourceId}/group-by?dimension=Product&from=...&to=...
        [HttpGet("{dataSourceId:int}/group-by")]
        public async Task<IActionResult> GetGroupBy(
            int dataSourceId,
            [FromQuery] SalesGroupByDimension dimension,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null,
            CancellationToken ct = default)
        {
            if (!await EnsureDataSourceOwnedAsync(dataSourceId, ct))
                return NotFound(new { error = "DataSource not found" });

            var result = await _svc.GetGroupedAsync(dataSourceId, dimension, from, to, ct);
            return Ok(result);
        }
    }
}

