using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Orders.DTOs.Orders;

namespace Orders.Tests.Api;

public class OrdersEndpointsTests : IClassFixture<OrdersApiFactory>
{
    private readonly HttpClient _client;

    public OrdersEndpointsTests(OrdersApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_WithEmptyName_Returns400_WithValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/orders", new { name = "", status = "new" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Name", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Create_Valid_Returns201_WithLocationAndServerFields()
    {
        var response = await _client.PostAsJsonAsync("/orders", new { name = "Test", status = "new" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var dto = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotEqual(Guid.Empty, dto!.Id);
        Assert.True(dto.Created > DateTime.MinValue);
    }

    [Fact]
    public async Task Get_MissingId_Returns404()
    {
        var response = await _client.GetAsync($"/orders/by-id/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBatch_Empty_Returns400_WithProblemDetails()
    {
        var response = await _client.PatchAsJsonAsync("/orders", Array.Empty<object>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("At least one order is required.", problem!.Detail);
    }

    [Fact]
    public async Task UpdateBatch_WithMissingId_Returns404_AndKeepsExistingUnchanged()
    {
        var created = await CreateOrderAsync("Keep", "new");

        var payload = new[]
        {
            new { id = created.Id, name = "Changed", status = "paid" },
            new { id = Guid.NewGuid(), name = "Ghost", status = "paid" }
        };
        var response = await _client.PatchAsJsonAsync("/orders", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var after = await _client.GetFromJsonAsync<OrderDto>($"/orders/by-id/{created.Id}");
        Assert.Equal("Keep", after!.Name);
        Assert.Equal("new", after.Status);
    }

    [Fact]
    public async Task Delete_RemovesOrder_ThenReturns404()
    {
        var created = await CreateOrderAsync("ToDelete", "new");

        var first = await _client.DeleteAsync($"/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await _client.DeleteAsync($"/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    private async Task<OrderDto> CreateOrderAsync(string name, string status)
    {
        var response = await _client.PostAsJsonAsync("/orders", new { name, status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
