using JaneERP.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/work-orders")]
[Authorize]
public class WorkOrdersController : ControllerBase
{
    private readonly ApiWorkOrderRepository _repo;

    public WorkOrdersController(ApiWorkOrderRepository repo) => _repo = repo;

    [HttpGet]
    public IActionResult GetWorkOrders([FromQuery] string? status)
        => Ok(_repo.GetWorkOrders(status));

    [HttpGet("{id:int}")]
    public IActionResult GetWorkOrder(int id)
    {
        var wo = _repo.GetDetail(id);
        return wo == null ? NotFound() : Ok(wo);
    }
}
