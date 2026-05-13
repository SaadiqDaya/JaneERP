using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ApiProductRepository _repo;
    private readonly CompanyContext        _ctx;

    public InventoryController(ApiProductRepository repo, CompanyContext ctx)
    {
        _repo = repo;
        _ctx  = ctx;
    }

    [HttpGet]
    public IActionResult Search([FromQuery] string? q, [FromQuery] int page = 1)
    {
        var (items, total) = _repo.Search(q, page);
        return Ok(new { items, total, page });
    }

    [HttpGet("low-stock")]
    public IActionResult LowStock([FromQuery] int page = 1)
    {
        var (items, total) = _repo.GetLowStock(page);
        return Ok(new { items, total, page });
    }

    [HttpGet("{productId:int}/stock")]
    public IActionResult StockByLocation(int productId)
        => Ok(_repo.GetStockByLocation(productId));

    [HttpGet("{productId:int}/history")]
    public IActionResult GetHistory(int productId, [FromQuery] int page = 1)
    {
        var (items, total, productName, sku) = _repo.GetTransactionHistory(productId, page);
        return Ok(new { items, total, page, productName, sku });
    }

    [HttpPost("{productId:int}/adjust")]
    public IActionResult Adjust(int productId, [FromBody] StockAdjustRequest req)
    {
        if (req.Qty == 0)
            return BadRequest(new { error = "Quantity cannot be zero." });
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { error = "Reason is required." });

        _repo.AdjustStock(productId, req.Qty, req.Reason, _ctx.Username);
        return Ok(new { success = true });
    }
}
