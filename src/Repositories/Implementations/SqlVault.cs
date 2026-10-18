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
    
    public bool? Update(Order order)
    {
        var existingOrder = _dbContext.Orders.FirstOrDefault(o => o.Id == order.Id);
        if (existingOrder is null) return false;
        
        existingOrder.Name = order.Name;
        existingOrder.Status = order.Status;
        
        var res = _dbContext.SaveChanges();
        return res > 0  ? true : false;
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