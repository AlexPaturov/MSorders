using System.Linq.Expressions;

namespace Orders.DTOs.Mappers;

public static class OrderMappings
{
    // Единственный источник правды: маппинг описан один раз как expression tree.
    // EF Core транслирует его в SELECT только нужных колонок.
    private static readonly Expression<Func<Order, OrderDto>> ProjectionExpr = o => new OrderDto
    {
        Id = o.Id,
        Name = o.Name,
        Status = o.Status,
        Created = o.Created
    };
    
    // Тот же expression, скомпилированный один раз, для in-memory маппинга.
    // Две версии маппинга разойтись не могут: это буквально один и тот же код.
    private static readonly Func<Order, OrderDto> Compiled = ProjectionExpr.Compile();
 
    /// <summary>In-memory: уже загруженная сущность -> DTO.</summary>
    public static OrderDto ToDto(this Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Compiled(order);
    }
 
    /// <summary>In-memory: коллекция сущностей -> DTO.</summary>
    public static IEnumerable<OrderDto> ToDtos(this IEnumerable<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        return orders.Select(Compiled);
    }
    
    /// <summary>
    /// На уровне запроса: в БД уходит SELECT только Id, Name, Status, Created.
    /// Использование: await db.Orders.Where(...).ProjectToDto().ToListAsync(ct);
    /// </summary>
    public static IQueryable<OrderDto> ProjectToDto(this IQueryable<Order> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Select(ProjectionExpr);
    }
}