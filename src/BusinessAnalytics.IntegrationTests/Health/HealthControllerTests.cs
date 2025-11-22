using System.Net;
using System.Threading.Tasks;
using BusinessAnalytics.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace BusinessAnalytics.IntegrationTests.Health;

public class HealthControllerTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOkStatus()
    {
        var response = await _client.GetAsync("api/v1/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }
}

