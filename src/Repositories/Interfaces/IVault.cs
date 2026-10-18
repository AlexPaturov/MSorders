namespace Orders.Repositories.Interfaces;

public interface IVault
{
    public Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(CancellationToken cancellationToken = default);
    
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    public Task<IReadOnlyCollection<Order>?> UpdateBatchAsync(IReadOnlyCollection<Order> orders, CancellationToken cancellationToken = default);
    
    public Task<bool?> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    public Task<bool?> InsertAsync(Order order, CancellationToken cancellationToken = default);
}
