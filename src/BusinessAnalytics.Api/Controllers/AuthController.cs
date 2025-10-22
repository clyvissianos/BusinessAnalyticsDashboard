using BusinessAnalytics.Application.DTOs;
using BusinessAnalytics.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static BusinessAnalytics.Application.DTOs.AuthDtos;

namespace BusinessAnalytics.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _config = config;
        _roleManager = roleManager;
    }

    [HttpPost("dev-create-admin")]
    [AllowAnonymous]
    public async Task<IActionResult> DevCreateAdmin(RegisterRequest req)
    {
        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, DisplayName = req.DisplayName };
        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);
        await _userManager.AddToRoleAsync(user, "Admin");
        return Ok(new { message = "Admin created", userId = user.Id });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (!await _roleManager.RoleExistsAsync(req.Role))
            return BadRequest(new { error = "Invalid role" });

        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, DisplayName = req.DisplayName };
        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, req.Role);
        return Ok(new { message = "User created", userId = user.Id });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null || !(await _userManager.CheckPasswordAsync(user, req.Password)))
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),                    // JWT 'sub'
            new(ClaimTypes.NameIdentifier, user.Id),                      // .NET 'nameidentifier'
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.Email ?? user.UserName ?? user.Id), // human-readable
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(double.Parse(jwt["ExpiresMinutes"]!));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse(tokenStr, expires);
    }
}

