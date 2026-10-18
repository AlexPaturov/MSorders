namespace Orders.Repositories.Implementations;

public class SqlVault: IVault
{
    private readonly SqlDbContext _dbContext;
    
    public SqlVault(SqlDbContext dbContext)
    {
        _dbContext  = dbContext;
    }
    
    public ReadOnlyDictionary<Guid, Order> GetAll()
    {
        return new  ReadOnlyDictionary<Guid, Order>(_dbContext.Orders.ToDictionary(o => o.Id));
    }

    public Order? GetById(Guid id)
    {
        return _dbContext.Orders.FirstOrDefault(o => o.Id == id);
    }

    public bool? Insert(Order order)
    {
        if (order.Id == Guid.Empty)
            order.Id = Guid.NewGuid();

        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();
        return true;
    }
    
    public IReadOnlyCollection<Order>? UpdateBatch(IReadOnlyCollection<Order> orders)
    {
        var incoming = orders.ToDictionary(o => o.Id);
        var existing = _dbContext.Orders
            .Where(o => incoming.Keys.Contains(o.Id))
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

        _dbContext.SaveChanges();
        return existing.Values.ToList();
    }

    public bool? Delete(Guid id)
    {
        var existingOrder = _dbContext.Orders.FirstOrDefault(o => o.Id == id);
        if (existingOrder is null) return false;

        _dbContext.Orders.Remove(existingOrder);
        _dbContext.SaveChanges();
        return true;
    }


}