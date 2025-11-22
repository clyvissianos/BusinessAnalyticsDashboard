using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Persistence;
using BusinessAnalytics.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusinessAnalytics.IntegrationTests.Analytics;

public class AnalyticsControllerTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;
    private readonly HttpClient _client;

    public AnalyticsControllerTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -------------------
    // Helper: Create user & get token
    // -------------------
    private async Task<string> CreateUserAndLoginAsync()
    {
        const string email = "test.analytics@example.com";
        const string password = "Test1234!";
        const string role = "Analyst";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1) Ensure role exists
            if (!await roleManager.RoleExistsAsync(role))
            {
                var r = await roleManager.CreateAsync(new IdentityRole(role));
                r.Succeeded.Should().BeTrue("Role should be created");
            }

            // 2) Ensure user exists
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = "Analytics User"
                };

                var createUser = await userManager.CreateAsync(user, password);
                createUser.Succeeded.Should().BeTrue("User must be created");
            }

            // 3) Ensure user is in role
            if (!await userManager.IsInRoleAsync(user, role))
            {
                var addRole = await userManager.AddToRoleAsync(user, role);
                addRole.Succeeded.Should().BeTrue("User must be assigned Analyst role");
            }
        }

        var loginRequest = new { email, password };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        json.Should().NotBeNull();
        json!.Should().ContainKey("token");

        return json["token"];
    }

    // -------------------
    // Helper: Seed a DataSource + one FactSales row
    // -------------------
    private async Task<int> SeedSimpleSalesAsync(string userEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(userEmail);
        user.Should().NotBeNull("User must exist for datasource owner");

        // Create a datasource
        var ds = new DataSource
        {
            Name = "Test Sales",
            OwnerId = user!.Id,
            Type = DataSourceType.Sales
        };
        db.DataSources.Add(ds);
        await db.SaveChangesAsync();

        // Create DimDate
        var date = new DateOnly(2024, 1, 10);
        var dateKey = DimDate.ToDateKey(date);

        var dd = new DimDate
        {
            DateKey = dateKey,
            Date = date,
            Year = 2024,
            Quarter = 1,
            Month = 1,
            MonthName = "January",
            Day = 10,
            IsoWeek = 2
        };
        db.DimDates.Add(dd);

        // Create DimProduct + DimCustomer
        var p = new DimProduct { ProductName = "Cola" };
        var c = new DimCustomer { CustomerName = "Alice" };
        db.DimProducts.Add(p);
        db.DimCustomers.Add(c);
        await db.SaveChangesAsync();

        // Create a FactSales row
        var fs = new FactSales
        {
            DataSourceId = ds.Id,
            DateKey = dateKey,
            ProductKey = p.ProductKey,
            CustomerKey = c.CustomerKey,
            Amount = 123.45m
        };
        db.FactSales.Add(fs);
        await db.SaveChangesAsync();

        return ds.Id;
    }

    // -------------------
    // Test 1: No token → 401
    // -------------------
    [Fact]
    public async Task Summary_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/analytics/sales/1/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------
    // Test 2: With token → 200 + JSON
    // -------------------
    [Fact]
    public async Task Summary_WithValidToken_Returns200AndData()
    {
        // 1) Create user + get token
        var token = await CreateUserAndLoginAsync();
        var email = "test.analytics@example.com";

        // 2) Seed a datasource with data owned by the same user
        var dsId = await SeedSimpleSalesAsync(email);

        // 3) Auth header
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // 4) Act
        var response = await _client.GetAsync($"/api/v1/analytics/sales/{dsId}/summary");

        // 5) Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        json.Should().NotBeNull();
        json!.Should().ContainKey("total");
        json.Should().ContainKey("average");

        // Optional deeper assertions if you want:
        json["total"].ToString().Should().Be("123.45");
    }
}

