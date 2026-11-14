using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Orders.Tests.Api;

public class OrdersApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orders_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    
    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected virtual void ConfigureTestServices(IServiceCollection services){}

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Databases:ActiveDatabase", "Postgres");
        builder.UseSetting("Databases:Postgres:ConnectionString", _postgres.GetConnectionString());
        builder.UseSetting("Auth:Jwt:SigningKey", TestJwt.SigningKey);
        builder.UseSetting("Auth:Jwt:Issuer", TestJwt.Issuer);
        builder.UseSetting("Auth:Jwt:Audience", TestJwt.Audience);
        builder.ConfigureServices(ConfigureTestServices);
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.CreateToken());
        return client;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
