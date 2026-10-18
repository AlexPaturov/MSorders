using Microsoft.AspNetCore.Mvc;
using Orders.DTOs;
using Orders.DTOs.Mappers;

namespace Orders.Controllers;

[ApiController]
[Route("[controller]")]
public class OrdersController : ControllerBase
{
    private IVault _orders;
    
    public  OrdersController(IVault orders)
    {
        _orders  = orders;
    }
        
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var orders = (await _orders.GetAllAsync(cancellationToken)).Values
            .Select(OrderMappings.ToDto)
            .ToList();

        return Ok(orders);
    }
    
    [HttpGet("by-id/{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(id, cancellationToken);

        return order is null ? NotFound() : Ok(OrderMappings.ToDto(order));
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateBatch([FromBody] List<BatchUpdateOrderDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos is null || dtos.Count == 0)
            return BadRequest("At least one order is required.");

        var updated = await _orders.UpdateBatchAsync(dtos.Select(d => d.ToEntity()).ToList(), cancellationToken);
        if (updated is null)
            return NotFound();

        return Ok(updated.Select(OrderMappings.ToDto));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await _orders.DeleteAsync(id, cancellationToken) == true ? NoContent() : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        var order = dto.ToEntity();
        await _orders.InsertAsync(order, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order.ToDto());
    }

}
