using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly ApiPurchaseOrderRepository _repo;
    private readonly CompanyContext             _ctx;

    public PurchaseOrdersController(ApiPurchaseOrderRepository repo, CompanyContext ctx)
    {
        _repo = repo;
        _ctx  = ctx;
    }

    [HttpGet]
    public IActionResult GetOrders([FromQuery] string? status)
        => Ok(_repo.GetOrders(status));

    [HttpGet("{id:int}")]
    public IActionResult GetOrder(int id)
    {
        var po = _repo.GetOrderDetail(id);
        return po == null ? NotFound() : Ok(po);
    }

    [HttpPost("{id:int}/receive")]
    public IActionResult ReceiveItems(int id, [FromBody] ReceiveItemsRequest req)
    {
        if (!req.Items.Any())
            return BadRequest(new { error = "No items provided." });

        _repo.ReceiveItems(id, req.Items, _ctx.Username);
        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/duplicate")]
    public IActionResult Duplicate(int id)
    {
        var newId = _repo.DuplicatePO(id, _ctx.Username);
        return Ok(new { poid = newId });
    }
}
