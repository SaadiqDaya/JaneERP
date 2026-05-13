using JaneERP.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ApiProductRepository _repo;

    public InventoryController(ApiProductRepository repo) => _repo = repo;

    [HttpGet]
    public IActionResult Search([FromQuery] string? q, [FromQuery] int page = 1)
    {
        var (items, total) = _repo.Search(q, page);
        return Ok(new { items, total, page });
    }

    [HttpGet("{productId:int}/stock")]
    public IActionResult StockByLocation(int productId)
        => Ok(_repo.GetStockByLocation(productId));
}
