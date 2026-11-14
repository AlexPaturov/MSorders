using Microsoft.EntityFrameworkCore;

namespace Orders.Repositories.Implementations;

public class PostgresVault: IVault
{
    private readonly PostgresDbContext _dbContext;
    private readonly ILogger<PostgresVault> _logger;

    public PostgresVault(PostgresDbContext  dbContext, ILogger<PostgresVault> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    
    public async Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(Guid ownerId,CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.Orders.ToListAsync(cancellationToken);
        return new  ReadOnlyDictionary<Guid, Order>(orders.ToDictionary(o => o.Id));
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
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
        {
            _logger.LogWarning("Batch update rejected; missing orders: {MissingIds}", missing);
            return null;
        }

        foreach (var order in orders)
        {
            var target = existing[order.Id];
            target.Name = order.Name;
            target.Status = order.Status;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated {Count} order(s)", orders.Count);
        return existing.Values.ToList();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existingOrder = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (existingOrder is null)
        {
            _logger.LogWarning("Order {OrderId} not found for deletion", id);
            return false;
        }

        _dbContext.Orders.Remove(existingOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted order {OrderId}", id);
        return true;
    }

    public async Task<bool> InsertAsync(Guid ownerId, Order order, CancellationToken cancellationToken = default)
    {
        if (order.Id == Guid.Empty)
            order.Id = Guid.NewGuid();
        
        order.OwnerId = ownerId;
        
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Inserted order {OrderId}", order.Id);
        return true;
    }
}