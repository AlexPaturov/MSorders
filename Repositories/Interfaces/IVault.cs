namespace Orders.Services.Interfaces;

public interface IVault
{
    public ReadOnlyDictionary<Guid, Order> GetAll();
    
    public Order? GetById(Guid id);
    
    public bool? Update(Order order);
    
    public bool? Delete(Order order);
    
    public bool? Insert(Order order);
}