using System.ComponentModel.DataAnnotations;
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

    [HttpPut("{id}")]
    public IActionResult PatchById(Guid id, [FromBody, Required] Order order)
    {
        if (id != order.Id)
            return BadRequest("Invalid id");
        
        return _orders.Update(order) ?? false ? Ok() : NoContent();
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