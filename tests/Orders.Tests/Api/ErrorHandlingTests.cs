using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Orders.Tests.Api;

public class ErrorHandlingTests : IClassFixture<ThrowingVaultFactory>
{
    private readonly HttpClient _client;

    public ErrorHandlingTests(ThrowingVaultFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_WhenVaultThrows_Returns500_ProblemDetails_WithoutStackTrace()
    {
        var response = await _client.GetAsync("/orders/all");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("InvalidOperationException", raw);

        var problem = JsonSerializer.Deserialize<ProblemDetails>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(500, problem!.Status);
    }
}
