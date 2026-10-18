using System.ComponentModel.DataAnnotations;

namespace Orders.DTOs;

public class BatchUpdateOrderDto
{
    public Guid Id { get; set; }
    
    [Required, MaxLength(200)]
    public string Name  { get; set; }
    
    [Required, MaxLength(50)]
    public string Status   { get; set; }    
}