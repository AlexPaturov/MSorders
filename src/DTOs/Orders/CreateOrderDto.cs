using System.ComponentModel.DataAnnotations;

namespace Orders.DTOs.Orders;

/// <summary>Данные для создания заказа.</summary>
public class CreateOrderDto
{
    /// <summary>Название заказа (обязательно, до 200 символов).</summary>
    [Required, MaxLength(200)]
    public string Name  { get; set; } = string.Empty;
    
    /// <summary>Статус заказа (обязательно, до 50 символов).</summary>
    [Required, MaxLength(50)]
    public string Status   { get; set; } = string.Empty;    
}
