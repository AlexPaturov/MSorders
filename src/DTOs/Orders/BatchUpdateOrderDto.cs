using System.ComponentModel.DataAnnotations;

namespace Orders.DTOs.Orders;

/// <summary>Элемент пакетного обновления заказов.</summary>
public class BatchUpdateOrderDto
{
    /// <summary>Идентификатор обновляемого заказа.</summary>
    public Guid Id { get; set; }
    
    /// <summary>Новое название (обязательно, до 200 символов).</summary>
    [Required, MaxLength(200)]
    public string Name  { get; set; } = string.Empty;
    
    /// <summary>Новый статус (обязательно, до 50 символов).</summary>
    [Required, MaxLength(50)]
    public string Status   { get; set; } = string.Empty;    
}
