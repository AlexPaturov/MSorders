using Microsoft.EntityFrameworkCore;

namespace Orders.Repositories.Implementations;

public class SqlVault: IVault
{
    private readonly SqlDbContext _dbContext;
    
    public SqlVault(SqlDbContext dbContext)
    {
        _dbContext  = dbContext;
    }
    
    public async Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.Orders.ToListAsync(cancellationToken);
        return new  ReadOnlyDictionary<Guid, Order>(orders.ToDictionary(o => o.Id));
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<bool?> InsertAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Id == Guid.Empty)
            order.Id = Guid.NewGuid();

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
    
    public async Task<IReadOnlyCollection<Order>?> UpdateBatchAsync(IReadOnlyCollection<Order> orders, CancellationToken cancellationToken = default)
    {
        var incoming = orders.ToDictionary(o => o.Id);
        var existing = (await _dbContext.Orders
                .Where(o => incoming.Keys.Contains(o.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(o => o.Id);

        var missing = incoming.Keys.Where(id => !existing.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            return null;

        foreach (var order in orders)
        {
            var target = existing[order.Id];
            target.Name = order.Name;
            target.Status = order.Status;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing.Values.ToList();
    }

    public async Task<bool?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existingOrder = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (existingOrder is null) return false;

        _dbContext.Orders.Remove(existingOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }


}
