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
    public IActionResult GetAll()
    {
        var orders = _orders.GetAll().Values
            .Select(OrderMappings.ToDto)
            .ToList();

        return Ok(orders);
    }
    
    [HttpGet("by-id/{id}")]
    public IActionResult GetById(Guid id)
    {
        var order = _orders.GetById(id);

        return order is null ? NotFound() : Ok(OrderMappings.ToDto(order));
    }

    [HttpPatch]
    public IActionResult UpdateBatch([FromBody] List<BatchUpdateOrderDto> dtos)
    {
        if (dtos is null || dtos.Count == 0)
            return BadRequest("At least one order is required.");

        var updated = _orders.UpdateBatch(dtos.Select(d => d.ToEntity()).ToList());
        if (updated is null)
            return NotFound();

        return Ok(updated.Select(OrderMappings.ToDto));
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        return _orders.Delete(id) == true ? NoContent() : NotFound();
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateOrderDto dto)
    {
        var order = dto.ToEntity();
        _orders.Insert(order);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order.ToDto());
    }

}