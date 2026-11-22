using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure;
using Orders.Models;
using Xunit.Abstractions;

namespace Orders.Tests.Api;

public class PaginationDemoTests : IClassFixture<OrdersApiFactory>
{
    private readonly OrdersApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public PaginationDemoTests(OrdersApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [Trait("Category", "ProblemDemo")]
    public async Task GetAll_WithPageSize_ReturnsPage(int count)
    {
        const int pageSize = 20;
        var ownerId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

            db.Orders.AddRange(Enumerable.Range(1, count).Select(i => new Order
            {
                Id = Guid.NewGuid(),
                Name = $"Demo order #{i}",
                Status = "Pending",
                Created = DateTime.UtcNow.AddMinutes(-i),
                OwnerId = ownerId
            }));

            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateAuthenticatedClient(ownerId);

        var response = await client.GetAsync($"/orders?page=1&pageSize={pageSize}");
        var json = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Seeded orders: {count}");
        _output.WriteLine($"Status: {(int)response.StatusCode}");
        _output.WriteLine($"Response bytes: {Encoding.UTF8.GetByteCount(json)}");
        _output.WriteLine(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(pageSize, root.GetProperty("pageSize").GetInt32());

        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(Math.Min(count, pageSize), items.Count);
    }
}
