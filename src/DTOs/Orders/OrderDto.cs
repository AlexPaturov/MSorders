namespace Orders.DTOs.Orders;

public class OrderDto
{
    public Guid Id { get; set; }
    public string Name  { get; set; } = string.Empty;
    public string Status   { get; set; } = string.Empty;    
    public DateTime Created  { get; set; }     
}