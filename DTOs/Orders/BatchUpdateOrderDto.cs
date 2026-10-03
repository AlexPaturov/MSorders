namespace Orders.DTOs;

public class BatchUpdateOrderDto
{
    public Guid Id { get; set; }
    public string Name  { get; set; }
    public string Status   { get; set; }    
    public DateTime Created  { get; set; }     
}