using JaneERP.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ApiCustomerRepository _repo;

    public CustomersController(ApiCustomerRepository repo) => _repo = repo;

    [HttpGet]
    public IActionResult Search([FromQuery] string? q, [FromQuery] int page = 1)
    {
        var (items, total) = _repo.Search(q, page);
        return Ok(new { items, total, page });
    }

    [HttpGet("{id:int}")]
    public IActionResult GetDetail(int id)
    {
        var detail = _repo.GetDetail(id);
        return detail == null ? NotFound() : Ok(detail);
    }
}
