namespace Orders.Models;

public class Order
{
    public Guid Id { get; set; }
    public string Name  { get; set; }
    public string Status   { get; set; }    
    public DateTime Created  { get; set; }     
}