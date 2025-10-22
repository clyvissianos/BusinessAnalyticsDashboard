using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
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
}

