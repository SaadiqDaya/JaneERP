using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ApiOrderRepository _repo;
    private readonly CompanyContext     _ctx;

    public OrdersController(ApiOrderRepository repo, CompanyContext ctx)
    {
        _repo = repo;
        _ctx  = ctx;
    }

    [HttpGet]
    public IActionResult GetOrders(
        [FromQuery] string?   status,
        [FromQuery] string?   q,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int       page = 1)
    {
        var (items, total) = _repo.GetOrders(status, q, from, to, page);
        return Ok(new { items, total, page });
    }

    [HttpGet("{id:int}")]
    public IActionResult GetOrder(int id)
    {
        var order = _repo.GetOrderDetail(id);
        return order == null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public IActionResult CreateOrder([FromBody] CreateOrderRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CustomerEmail))
            return BadRequest(new { error = "Customer email is required." });
        if (!req.Items.Any())
            return BadRequest(new { error = "At least one line item is required." });

        var id = _repo.CreateManualOrder(req, _ctx.Username);
        return CreatedAtAction(nameof(GetOrder), new { id }, new { salesOrderId = id });
    }

    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateStatusRequest req)
    {
        var validStatuses = new[] { "Draft", "Live", "WIP", "Packed", "Shipped", "Complete" };
        if (!validStatuses.Contains(req.Status))
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", validStatuses)}" });

        var updated = _repo.UpdateOrderStatus(id, req.Status, _ctx.Username);
        return updated ? Ok(new { success = true }) : NotFound();
    }

    [HttpGet("{id:int}/pick-list")]
    public IActionResult GetPickList(int id) => Ok(_repo.GetPickList(id));

    [HttpPatch("{id:int}/notes")]
    public IActionResult UpdateNotes(int id, [FromBody] UpdateNotesRequest req)
    {
        var updated = _repo.UpdateNotes(id, req.Notes);
        return updated ? Ok(new { success = true }) : NotFound();
    }
}
