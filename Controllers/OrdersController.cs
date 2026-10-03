using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

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
        return (_orders.GetAll().Count > 0) ? Ok(_orders.GetAll()) : NoContent();
    }
    
    [HttpGet("by-id/{id}")]
    public IActionResult GetById(Guid id)
    {
        return (_orders.GetById(id) is null) ? NotFound() : Ok(_orders.GetById(id));
    }

    [HttpPut("{id}")]
    public IActionResult PatchById(Guid id, [FromBody, Required] Order order)
    {
        if (id != order.Id)
            return BadRequest("Invalid id");
        
        return _orders.Update(order) ?? false ? Ok() : NoContent();
    }

    [HttpDelete]
    public IActionResult Delete([FromBody, Required] Order order)
    {
        // TODO validation
        return _orders.Delete(order) ?? false ? NoContent() : NotFound();
    }

    [HttpPost]
    public IActionResult Put([FromBody, Required] Order order)
    {
        // TODO validation
        return _orders.Insert(order) ?? false ? Ok() : NoContent();
    }

}