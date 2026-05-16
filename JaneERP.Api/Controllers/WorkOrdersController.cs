using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using JaneERP.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/work-orders")]
[Authorize(Roles = "Admin,Manager,Warehouse")]
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

    /// <summary>BOM ingredients with required quantities for a work order.</summary>
    [HttpGet("{id:int}/bom-preview")]
    public IActionResult GetBomPreview(int id)
    {
        try { return Ok(_repo.GetBomPreview(id)); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>
    /// Available lots for each BOM ingredient, FEFO sorted.
    /// Used to build the Go-Live lot-selection UI.
    /// </summary>
    [HttpGet("{id:int}/lot-availability")]
    public IActionResult GetLotAvailability(int id)
    {
        try { return Ok(_repo.GetLotAvailability(id)); }
        catch (InvalidOperationException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// Go Live: lock specific inventory lots and transition WO Pending → Live.
    /// </summary>
    [HttpPost("{id:int}/go-live")]
    public IActionResult GoLive(int id, [FromBody] GoLiveRequest req)
    {
        if (req?.Reservations == null || req.Reservations.Count == 0)
            return BadRequest(new { error = "At least one lot reservation is required." });

        try
        {
            var lots = req.Reservations.Select(r => new LotReservation
            {
                LotID    = r.LotID,
                PartID   = r.PartID,
                Quantity = r.Quantity,
            });
            _repo.GoLive(id, lots, _ctx.Username ?? "api");
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Verify &amp; Complete: record actual yields, deduct lots, add finished goods, calculate COGS.
    /// </summary>
    [HttpPost("{id:int}/complete")]
    public IActionResult CompleteWorkOrder(int id, [FromBody] CompleteWORequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });
        if (req.CompletedQty < 0) return BadRequest(new { error = "CompletedQty must be ≥ 0." });

        try
        {
            _repo.CompleteWorkOrder(id, new CompleteWorkOrderRequest
            {
                CompletedQty = req.CompletedQty,
                ScrapQty     = req.ScrapQty,
                ScrapReason  = req.ScrapReason,
                LocationID   = req.LocationID,
                Notes        = req.Notes,
            }, _ctx.Username ?? "api");
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    /// <summary>Legacy status patch — only Pending, Live, and Cancelled. Use POST /complete for completion; cooking auto-advances to InProgress.</summary>
    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateWOStatusRequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });

        if (req.Status is "Complete" or "Completed")
            return BadRequest(new { error = "Use POST /api/work-orders/{id}/complete to complete a work order — this enforces stock deduction and lot tracking." });
        if (req.Status is "InProgress")
            return BadRequest(new { error = "Work orders advance to InProgress automatically when a cook session is created." });

        var valid = new[] { "Pending", "Live", "Cancelled" };
        if (!valid.Contains(req.Status))
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", valid)}" });

        try
        {
            var updated = _repo.UpdateStatus(id, req.Status, _ctx.Username);
            return updated ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }
}

public record UpdateWOStatusRequest(string Status);
