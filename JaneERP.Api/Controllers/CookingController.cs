using JaneERP.Api.Data;
using JaneERP.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/cooking")]
[Authorize(Roles = "Admin,Manager,Warehouse")]
public class CookingController : ControllerBase
{
    private readonly ApiCookingRepository _repo;
    public CookingController(ApiCookingRepository repo) => _repo = repo;

    private string CurrentUser =>
        User.FindFirstValue(ClaimTypes.Name) ?? "mobile";

    /// <summary>List all open cook sessions with step progress counts.</summary>
    [HttpGet("sessions")]
    public IActionResult GetSessions() => Ok(_repo.GetOpenSessions());

    /// <summary>Get a single session with its ingredients and per-batch steps.</summary>
    [HttpGet("sessions/{id:int}")]
    public IActionResult GetSession(int id)
    {
        var session = _repo.GetSession(id);
        if (session == null) return NotFound(new { error = "Session not found." });
        return Ok(session);
    }

    /// <summary>Flask configs and batch-loss presets for the cook session create panel.</summary>
    [HttpGet("manufacturing-settings")]
    public IActionResult GetManufacturingSettings() => Ok(_repo.GetManufacturingSettings());

    /// <summary>In-progress work orders available for a new cook session.</summary>
    [HttpGet("work-orders")]
    public IActionResult GetWorkOrders() => Ok(_repo.GetPendingWorkOrders());

    /// <summary>Create a new cook session from one or more work orders.</summary>
    [HttpPost("sessions")]
    public IActionResult CreateSession([FromBody] CreateCookSessionRequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });
        if (string.IsNullOrWhiteSpace(req.SessionName) || req.WorkOrderIds.Count == 0)
            return BadRequest(new { error = "SessionName and at least one WorkOrderId are required." });
        try
        {
            int id = _repo.CreateSession(req.SessionName, req.WorkOrderIds,
                req.BatchLossPercent, CurrentUser);
            return Ok(new { cookSessionId = id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Mark a single (WorkOrder × Part) step as done.</summary>
    [HttpPost("steps/{stepId:int}/done")]
    public IActionResult MarkStepDone(int stepId)
    {
        _repo.MarkStepDone(stepId, CurrentUser);
        return Ok();
    }

    /// <summary>Mark all steps for one ingredient across all batches in the session as done.</summary>
    [HttpPost("sessions/{id:int}/ingredients/{partId:int}/done")]
    public IActionResult MarkAllIngredientDone(int id, int partId)
    {
        _repo.MarkAllIngredientStepsDone(id, partId, CurrentUser);
        return Ok();
    }

    /// <summary>Complete a cook session. Returns 409 if steps are pending and forceComplete is false.</summary>
    [HttpPost("sessions/{id:int}/complete")]
    public IActionResult CompleteSession(int id, [FromBody] CompleteCookSessionRequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });
        try
        {
            _repo.CompleteSession(id, req.ForceComplete, CurrentUser);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
