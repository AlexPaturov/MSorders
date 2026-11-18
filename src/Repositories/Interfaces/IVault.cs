namespace Orders.Repositories.Interfaces;

public interface IVault
{
    public Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(Guid ownerId,CancellationToken cancellationToken = default);
    
    public Task<Order?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);
    
    public Task<IReadOnlyCollection<Order>?> UpdateBatchAsync(Guid ownerId,IReadOnlyCollection<Order> orders, CancellationToken cancellationToken = default);
    
    public Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);
    
    public Task<bool> InsertAsync(Guid ownerId, Order order, CancellationToken cancellationToken = default);
}
