namespace Orders.Repositories.Interfaces;

public interface IVault
{
    public Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(Guid ownerId,CancellationToken cancellationToken = default);
    
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    public Task<IReadOnlyCollection<Order>?> UpdateBatchAsync(IReadOnlyCollection<Order> orders, CancellationToken cancellationToken = default);
    
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    public Task<bool> InsertAsync(Guid ownerId, Order order, CancellationToken cancellationToken = default);
}
