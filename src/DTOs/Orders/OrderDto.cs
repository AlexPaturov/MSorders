namespace Orders.DTOs.Orders;

/// <summary>Заказ, возвращаемый API.</summary>
public class OrderDto
{
    /// <summary>Идентификатор заказа.</summary>
    public Guid Id { get; set; }

    /// <summary>Название заказа.</summary>
    public string Name  { get; set; } = string.Empty;

    /// <summary>Статус заказа.</summary>
    public string Status   { get; set; } = string.Empty;    

    /// <summary>Дата и время создания.</summary>
    public DateTime Created  { get; set; }     
}
