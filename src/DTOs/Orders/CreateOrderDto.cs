using System.ComponentModel.DataAnnotations;

namespace Orders.DTOs.Orders;

public class CreateOrderDto
{
    [Required, MaxLength(200)]
    public string Name  { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string Status   { get; set; } = string.Empty;    
}