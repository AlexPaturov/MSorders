using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Models;
using Orders.Repositories.Interfaces;

namespace Orders.Tests.Api;

public class ThrowingVaultFactory : OrdersApiFactory
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IVault>();
        services.AddScoped<IVault, ThrowingVault>();
    }
}

internal sealed class ThrowingVault : IVault
{
    public ReadOnlyDictionary<Guid, Order> GetAll() => throw new InvalidOperationException("boom");

    public Order? GetById(Guid id) => throw new InvalidOperationException("boom");

    public bool? Insert(Order order) => throw new InvalidOperationException("boom");

    public IReadOnlyCollection<Order>? UpdateBatch(IReadOnlyCollection<Order> orders) => throw new InvalidOperationException("boom");

    public bool? Delete(Guid id) => throw new InvalidOperationException("boom");
}
