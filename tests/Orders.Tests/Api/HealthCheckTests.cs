using System.Net;

namespace Orders.Tests.Api;

public class HealthCheckTests : IClassFixture<OrdersApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(OrdersApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }
}
