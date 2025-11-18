using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static BusinessAnalytics.Application.DTOs.DataSourceDtos;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/v1/datasources")]
[Authorize] // Viewer/Analyst/Admin can read; creation requires Analyst/Admin
public class DataSourcesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DataSourcesController(AppDbContext db) => _db = db;

    private string? CurrentUserId =>
    User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? User.FindFirstValue("sub");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DataSourceResponse>>> List()
    {
        var uid = CurrentUserId!;
        var items = await _db.DataSources
            .Where(x => x.OwnerId == uid)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new DataSourceResponse(x.Id, x.Name, x.Type, x.CreatedAtUtc))
            .ToListAsync();

        return items;
    }

    [HttpPost]
    [Authorize(Roles = "Analyst,Admin")]
    public async Task<ActionResult<DataSourceResponse>> Create(DataSourceCreateRequest req)
    {
        var uid = CurrentUserId!;
        var ds = new DataSource { Name = req.Name, Type = req.Type, OwnerId = uid };
        _db.DataSources.Add(ds);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ds.Id }, new DataSourceResponse(ds.Id, ds.Name, ds.Type, ds.CreatedAtUtc));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DataSourceResponse>> GetById(int id)
    {
        var uid = CurrentUserId!;
        var ds = await _db.DataSources.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == uid);
        if (ds is null) return NotFound();
        return new DataSourceResponse(ds.Id, ds.Name, ds.Type, ds.CreatedAtUtc);
    }


    /// <summary>
    /// Downloads the Sales Template in CSV format.
    /// </summary>
    /// <remarks>
    /// ⚠️ **Character Encoding Notice**  
    /// The CSV file is encoded in **UTF-8 with BOM** (web standard).  
    ///  
    /// Excel for Windows may show unreadable characters if the file is opened by double-click.  
    ///  
    /// To open correctly in Excel:
    /// 1. Go to **Data → From Text/CSV**  
    /// 2. Select the file  
    /// 3. Choose **UTF-8** encoding  
    /// 4. Click **Load**  
    ///  
    /// For easiest use, prefer the **XLSX template**, which opens with correct Greek characters.
    /// </remarks>
    /// <returns>UTF-8 CSV Sales Template</returns>
    [HttpGet("~/api/v1/templates/sales.csv")]
    [AllowAnonymous]
    public IActionResult SalesTemplateCsv()
    {
        var csv =
        "Ημερομηνία,Προϊόν,Πελάτης,Ποσότητα,Ποσό\r\n" +
        "2025-01-01,Δείγμα,Πελάτης Α,1,100.00\r\n";

        // UTF-8 with BOM – correct for web + most tools
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = utf8Bom.GetBytes(csv);

        return File(bytes, "text/csv; charset=utf-8", "SalesTemplate.csv");
    }

    /// <summary>
    /// Downloads the Sales Template in Microsoft Excel (XLSX) format.
    /// </summary>
    /// <remarks>
    /// ✔ **Recommended for all users**  
    /// Fully compatible with Excel, Power BI, and business systems.  
    ///
    /// Contains columns:  
    /// - Ημερομηνία  
    /// - Προϊόν  
    /// - Πελάτης  
    /// - Ποσότητα  
    /// - Ποσό  
    /// </remarks>
    /// <returns>Excel XLSX sales template</returns>
    [HttpGet("~/api/v1/templates/sales.xlsx")]
    [AllowAnonymous]
    public IActionResult SalesTemplateXlsx()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sales");

        ws.Cell("A1").Value = "Ημερομηνία";
        ws.Cell("B1").Value = "Προϊόν";
        ws.Cell("C1").Value = "Πελάτης";
        ws.Cell("D1").Value = "Ποσότητα";
        ws.Cell("E1").Value = "Ποσό";

        ws.Cell("A2").Value = new DateTime(2025, 1, 1);
        ws.Cell("B2").Value = "Δείγμα";
        ws.Cell("C2").Value = "Πελάτης Α";
        ws.Cell("D2").Value = 1;
        ws.Cell("E2").Value = 100.00;

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "SalesTemplate.xlsx");
    }
}

