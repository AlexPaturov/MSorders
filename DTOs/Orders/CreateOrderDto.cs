namespace Orders.DTOs;

public class CreateOrderDto
{
    public string Name  { get; set; }
    public string Status   { get; set; }    
    public DateTime Created  { get; set; }     
}