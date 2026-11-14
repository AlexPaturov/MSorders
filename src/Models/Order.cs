using Microsoft.EntityFrameworkCore;

namespace Orders.Models;

[Index(nameof(OwnerId))]
public class Order
{
    /// <summary>Уникальный идентификатор заказа.</summary>
    public Guid Id { get; set; }

    /// <summary>Название заказа.</summary>
    public string Name  { get; set; } = string.Empty;

    /// <summary>Статус заказа.</summary>
    public string Status   { get; set; } = string.Empty;    

    /// <summary>Дата и время создания заказа.</summary>
    public DateTime Created  { get; set; }     

    /// <summary>Идентификатор пользователя-владельца заказа.</summary>
    public Guid OwnerId { get; set; }
}