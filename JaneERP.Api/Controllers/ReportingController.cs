using JaneERP.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Finance,Manager")]
public class ReportingController : ControllerBase
{
    private readonly ApiReportingRepository _repo;

    public ReportingController(ApiReportingRepository repo) => _repo = repo;

    /// <summary>Full inventory snapshot — current stock for every active product.</summary>
    [HttpGet("stock")]
    public IActionResult GetStock()
    {
        try { return Ok(_repo.GetStockSnapshot()); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Sales order list for a date range.</summary>
    [HttpGet("sales")]
    public IActionResult GetSales(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null)
    {
        var f = (from ?? DateTime.Today.AddDays(-30)).Date;
        var t = (to   ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

        try { return Ok(_repo.GetSalesReport(f, t)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>COGS report — completed work orders with cost of goods in a date range.</summary>
    [HttpGet("cogs")]
    public IActionResult GetCogs(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null)
    {
        var f = (from ?? DateTime.Today.AddDays(-30)).Date;
        var t = (to   ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

        try { return Ok(_repo.GetCogsReport(f, t)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Gross profit summary (revenue, COGS, gross profit) for a date range.</summary>
    [HttpGet("gross-profit")]
    public IActionResult GetGrossProfit(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null)
    {
        var f = (from ?? DateTime.Today.AddDays(-30)).Date;
        var t = (to   ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

        try { return Ok(_repo.GetGrossProfitSummary(f, t)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}
