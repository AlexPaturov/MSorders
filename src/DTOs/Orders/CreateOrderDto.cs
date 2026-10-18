using System.ComponentModel.DataAnnotations;

namespace Orders.DTOs;

public class CreateOrderDto
{
    [Required, MaxLength(200)]
    public string Name  { get; set; }
    
    [Required, MaxLength(50)]
    public string Status   { get; set; }    
}