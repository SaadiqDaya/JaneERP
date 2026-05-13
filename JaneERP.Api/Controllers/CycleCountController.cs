using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/cycle-count")]
[Authorize]
public class CycleCountController : ControllerBase
{
    private readonly ApiCycleCountRepository _repo;
    private readonly CompanyContext          _ctx;

    public CycleCountController(ApiCycleCountRepository repo, CompanyContext ctx)
    {
        _repo = repo;
        _ctx  = ctx;
    }

    [HttpGet("entries")]
    public IActionResult GetEntries([FromQuery] int? locationId)
        => Ok(_repo.GetEntries(locationId));

    [HttpPost("verify")]
    public IActionResult Verify([FromBody] VerifyRequest req)
    {
        if (req.ActualQty < 0)
            return BadRequest(new { error = "Actual quantity cannot be negative." });

        _repo.RecordVerification(
            req.ProductId, req.LocationId, req.SystemQty, req.ActualQty, _ctx.Username);

        return Ok(new { success = true });
    }
}
