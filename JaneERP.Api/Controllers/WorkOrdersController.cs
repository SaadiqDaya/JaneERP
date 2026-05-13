using JaneERP.Api.Data;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/work-orders")]
[Authorize]
public class WorkOrdersController : ControllerBase
{
    private readonly ApiWorkOrderRepository _repo;
    private readonly CompanyContext         _ctx;

    public WorkOrdersController(ApiWorkOrderRepository repo, CompanyContext ctx)
    {
        _repo = repo;
        _ctx  = ctx;
    }

    [HttpGet]
    public IActionResult GetWorkOrders([FromQuery] string? status)
        => Ok(_repo.GetWorkOrders(status));

    [HttpGet("{id:int}")]
    public IActionResult GetWorkOrder(int id)
    {
        var wo = _repo.GetDetail(id);
        return wo == null ? NotFound() : Ok(wo);
    }

    // Roles: admin, warehouse
    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateWOStatusRequest req)
    {
        var valid = new[] { "Pending", "InProgress", "Complete", "Cancelled" };
        if (!valid.Contains(req.Status))
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", valid)}" });

        var updated = _repo.UpdateStatus(id, req.Status, _ctx.Username);
        return updated ? Ok(new { success = true }) : NotFound();
    }
}

public record UpdateWOStatusRequest(string Status);
