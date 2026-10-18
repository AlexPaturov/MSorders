namespace Orders.Repositories.Interfaces;

public interface IVault
{
    public ReadOnlyDictionary<Guid, Order> GetAll();
    
    public Order? GetById(Guid id);
    
    public IReadOnlyCollection<Order>? UpdateBatch(IReadOnlyCollection<Order> orders);
    
    public bool? Delete(Guid id);
    
    public bool? Insert(Order order);
}