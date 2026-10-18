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
    public Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");

    public Task<bool?> InsertAsync(Order order, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");

    public Task<IReadOnlyCollection<Order>?> UpdateBatchAsync(IReadOnlyCollection<Order> orders, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");

    public Task<bool?> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
}
