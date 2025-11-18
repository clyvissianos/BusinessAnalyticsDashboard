using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Files;
using BusinessAnalytics.Infrastructure.Parsing;
using BusinessAnalytics.Infrastructure.Parsing.Preview;
using BusinessAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/v1/datasources/{dataSourceId:int}/upload")]
[Authorize(Roles = "Analyst,Admin")]
public class UploadsController : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".csv"];
    private readonly AppDbContext _db;
    private readonly IFileStorage _files;
    private readonly ISalesParser _salesParser;
    private readonly long _maxBytes;

    public UploadsController(AppDbContext db, IFileStorage files, ISalesParser salesParser, Microsoft.Extensions.Options.IOptions<BusinessAnalytics.Infrastructure.Files.UploadOptions> opts)
    {
        _db = db;
        _files = files;
        _maxBytes = opts.Value.MaxBytes;
        _salesParser = salesParser;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("User id not found in token.");

    public record MappingUpsertRequest(
    string Kind,                  // "Sales" (for now)
    string? SheetName,
    string Culture,
    Dictionary<string, string> Map // canonical → source header
);

    /// <summary>
    /// Upload a CSV file for the given DataSource. Creates a RawImport row with Status=Staged.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(60_000_000)] // server-side cap (~60MB); also see appsettings Kestrel limits below
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(int dataSourceId, IFormFile file, CancellationToken ct)
    {
        var uid = CurrentUserId!;
        var ds = await _db.DataSources.FirstOrDefaultAsync(x => x.Id == dataSourceId && x.OwnerId == uid, ct);
        if (ds is null) return NotFound(new { error = "DataSource not found" });

        if (file is null || file.Length == 0) return BadRequest(new { error = "Empty file" });
        if (file.Length > _maxBytes) return BadRequest(new { error = $"File too large. Max allowed {_maxBytes} bytes." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = "Only .csv files are supported in this milestone." });

        await using var stream = file.OpenReadStream();
        var path = await _files.SaveAsync(stream, file.FileName, ct);

        var import = new RawImport
        {
            DataSourceId = ds.Id,
            OriginalFilePath = path,
            Status = ImportStatus.Staged,
            Rows = 0,
            StartedAtUtc = DateTime.UtcNow
        };
        _db.RawImports.Add(import);
        await _db.SaveChangesAsync(ct);

        // Parsing will be implemented in Milestone 3.
        return Accepted(new
        {
            message = "File staged. Ready to parse in the next milestone.",
            importId = import.Id,
            import.Status,
            pathHint = Path.GetFileName(path) // don't leak full path to client
        });
    }

    /// <summary>
    /// Get RawImport status (owner-scoped).
    /// </summary>
    [HttpGet("~/api/v1/imports/{id:int}")]
    [Authorize] // any authenticated user can query their own imports
    public async Task<IActionResult> GetImport(int id, CancellationToken ct)
    {
        var uid = CurrentUserId!;
        var imp = await _db.RawImports
            .Include(x => x.DataSource)
            .FirstOrDefaultAsync(x => x.Id == id && x.DataSource.OwnerId == uid, ct);

        if (imp is null) return NotFound();

        return Ok(new
        {
            imp.Id,
            imp.Status,
            imp.Rows,
            imp.StartedAtUtc,
            imp.CompletedAtUtc,
            imp.ErrorMessage
        });
    }

    [HttpPost("~/api/v1/imports/{id:int}/preview")]
    [Authorize]
    public async Task<IActionResult> PreviewImport(int id, [FromQuery] string? sheet, [FromServices] IPreviewService preview, CancellationToken ct)
    {
        var uid = CurrentUserId!;
        var imp = await _db.RawImports.Include(x => x.DataSource)
            .FirstOrDefaultAsync(x => x.Id == id && x.DataSource.OwnerId == uid, ct);
        if (imp is null) return NotFound();

        var result = await preview.PreviewAsync(imp.OriginalFilePath, sheet);
        return Ok(result);
    }

    [HttpPut("~/api/v1/datasources/{id:int}/mapping")]
    [Authorize(Roles = "Analyst,Admin")]
    public async Task<IActionResult> UpsertMapping(int id, [FromBody] MappingUpsertRequest req, CancellationToken ct)
    {
        var uid = CurrentUserId!;
        var ds = await _db.DataSources.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
        if (ds is null) return NotFound();

        var mapping = await _db.DataSourceMappings.FirstOrDefaultAsync(x => x.DataSourceId == id, ct);
        var json = System.Text.Json.JsonSerializer.Serialize(req.Map);

        if (mapping is null)
        {
            mapping = new DataSourceMapping
            {
                DataSourceId = id,
                Kind = req.Kind,
                SheetName = req.SheetName ?? "",
                Culture = string.IsNullOrWhiteSpace(req.Culture) ? "el-GR" : req.Culture,
                ColumnMapJson = json
            };
            _db.DataSourceMappings.Add(mapping);
        }
        else
        {
            mapping.Kind = req.Kind;
            mapping.SheetName = req.SheetName ?? "";
            mapping.Culture = string.IsNullOrWhiteSpace(req.Culture) ? "el-GR" : req.Culture;
            mapping.ColumnMapJson = json;
            mapping.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Mapping saved." });
    }

    /// <summary>
    /// Parse a staged import and load data into FactSales / dimensions.
    /// </summary>
    /// <param name="id">RawImport Id.</param>
    [HttpPost("{id:int}/parse")]
    public async Task<IActionResult> Parse(int id, CancellationToken ct)
    {
        var uid = CurrentUserId;

        // Ownership + existence check
        var import = await _db.RawImports
            .Include(r => r.DataSource)
            .FirstOrDefaultAsync(r => r.Id == id && r.DataSource.OwnerId == uid, ct);

        if (import is null)
            return NotFound(new { error = "Import not found" });

        var result = await _salesParser.ParseAndImportAsync(id, ct);

        if (!result.Success)
        {
            return BadRequest(new
            {
                error = result.Error,
                rows = result.Rows
            });
        }

        return Ok(new
        {
            message = "Import parsed successfully.",
            rows = result.Rows,
            dataSourceId = import.DataSourceId,
            status = import.Status.ToString()
        });
    }

}

