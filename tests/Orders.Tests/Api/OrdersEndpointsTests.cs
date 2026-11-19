using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Orders.DTOs.Orders;

namespace Orders.Tests.Api;

public class OrdersEndpointsTests : IClassFixture<OrdersApiFactory>
{
    private readonly HttpClient _client;
    private readonly  OrdersApiFactory _factory;

    public OrdersEndpointsTests(OrdersApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
        _factory = factory;
    }

    [Fact]
    public async Task Get_All_WithoutToken_Returns401()
    {
        using var client = _factory.CreateClient(); // получаем неавторизованного клиента
        var response = await client.GetAsync("/orders/all");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithExpiredToken_Returns401()
    {
        var token = TestJwt.CreateToken(
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(-1));

        using var client = _factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/orders/all");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithTokenSignedByOtherKey_Returns401()
    {
        var token = TestJwt.CreateToken(
            signingKey: "1111111111111111111111111111111111111111111111111111111111111111");

        using var client = _factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/orders/all");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_UserSeesOnlyOwnOrders()
    {
        var own = await CreateOrderAsync("Own", "new");

        using var otherClient = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var others = await otherClient.GetFromJsonAsync<List<OrderDto>>("/orders/all");
        Assert.DoesNotContain(others!, order => order.Id == own.Id);

        var mine = await _client.GetFromJsonAsync<List<OrderDto>>("/orders/all");
        Assert.Contains(mine!, order => order.Id == own.Id);
    }

    [Fact]
    public async Task ForeignOrderId_Returns404_ForGetByIdAndDelete()
    {
        var own = await CreateOrderAsync("ForeignCheck", "new");

        using var otherClient = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var get = await otherClient.GetAsync($"/orders/by-id/{own.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var getProblem = await get.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Order not found.", getProblem!.Detail);

        var delete = await otherClient.DeleteAsync($"/orders/{own.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        var after = await _client.GetAsync($"/orders/by-id/{own.Id}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task UpdateBatch_WithForeignId_Returns404_WithoutPartialChanges()
    {
        var own = await CreateOrderAsync("Keep", "new");

        using var otherClient = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var payload = new[]
        {
            new { id = own.Id, name = "Changed", status = "paid" },
            new { id = Guid.NewGuid(), name = "Ghost", status = "paid" }
        };

        var response = await otherClient.PatchAsJsonAsync("/orders", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("One or more orders not found.", problem!.Detail);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(own.Id.ToString(), raw);

        var after = await _client.GetFromJsonAsync<OrderDto>($"/orders/by-id/{own.Id}");
        Assert.Equal("Keep", after!.Name);
        Assert.Equal("new", after.Status);
    }

    [Fact]
    public async Task GetAll_WithoutRequiredScope_Returns403()
    {
        var token = TestJwt.CreateToken(scope: "orders.read");
        using var client = _factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/orders/all");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private async Task<OrderDto> CreateOrderAsync(string name, string status) =>
        await CreateOrderAsync(_client, name, status);

    private static async Task<OrderDto> CreateOrderAsync(HttpClient client, string name, string status)
    {
        var response = await client.PostAsJsonAsync("/orders", new { name, status });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
