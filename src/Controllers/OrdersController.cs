using Microsoft.AspNetCore.Mvc;
using Orders.DTOs.Mappers;
using Orders.DTOs.Orders;

namespace Orders.Controllers;

/// <summary>Операции над заказами.</summary>
[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IVault _orders;
    
    public OrdersController(IVault orders)
    {
        _orders = orders;
    }

    /// <summary>Возвращает все заказы.</summary>
    /// <response code="200">Список заказов (возможно пустой).</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var orders = (await _orders.GetAllAsync(cancellationToken)).Values
            .Select(OrderMappings.ToDto)
            .ToList();

        return Ok(orders);
    }

    /// <summary>Возвращает заказ по идентификатору.</summary>
    /// <response code="200">Заказ найден.</response>
    /// <response code="404">Заказ не найден.</response>
    [HttpGet("by-id/{id}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, cancellationToken);

        return order is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: "Order not found.")
            : Ok(OrderMappings.ToDto(order));
    }

    /// <summary>Пакетно обновляет заказы (атомарно).</summary>
    /// <response code="200">Массив обновлённых заказов.</response>
    /// <response code="400">Пустой список.</response>
    /// <response code="404">Хотя бы один заказ не найден (изменения откатываются).</response>
    [HttpPatch]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBatch([FromBody] List<BatchUpdateOrderDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos is null || dtos.Count == 0)
            return Problem(
                detail: "At least one order is required.",
                statusCode: StatusCodes.Status400BadRequest);

        var updated = await _orders.UpdateBatchAsync(dtos.Select(d => d.ToEntity()).ToList(), cancellationToken);
        if (updated is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "One or more orders not found.");


        return Ok(updated.Select(OrderMappings.ToDto));
    }

    /// <summary>Удаляет заказ.</summary>
    /// <response code="204">Заказ удалён.</response>
    /// <response code="404">Заказ не найден.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await _orders.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Order not found.");
    }

    /// <summary>Создаёт заказ.</summary>
    /// <response code="201">Заказ создан; в теле — серверные поля, в заголовке — Location.</response>
    /// <response code="400">Ошибки валидации входных данных.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        var order = dto.ToEntity();
        await _orders.InsertAsync(order, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order.ToDto());
    }

}
