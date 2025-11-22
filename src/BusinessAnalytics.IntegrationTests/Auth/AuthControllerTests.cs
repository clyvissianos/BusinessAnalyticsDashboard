using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Persistence;
using BusinessAnalytics.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace BusinessAnalytics.IntegrationTests.Auth;

public class AuthControllerTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var request = new
        {
            email = "wrong@user.com",
            password = "InvalidPass123!"
        };

        var response = await _client.PostAsJsonAsync("api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        const string email = "test.user@example.com";
        const string password = "Test1234!";

        // ---------- Seed a user via Identity ----------
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Avoid duplicates on re-run
            var existing = await userManager.FindByEmailAsync(email);
            if (existing == null)
            {
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = "Test User"
                };

                var result = await userManager.CreateAsync(user, password);
                result.Succeeded.Should().BeTrue("test user should be created successfully");
            }

            await db.SaveChangesAsync();
        }

        // ---------- Act: call /auth/login ----------
        var loginRequest = new { email, password };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        json.Should().NotBeNull();
        json!.Keys.Should().Contain(k => k.Equals("token", StringComparison.OrdinalIgnoreCase));
    }
}

