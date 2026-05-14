using JaneERP.Api.Data;
using JaneERP.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Finance,Manager")]
public class AccountingController : ControllerBase
{
    private readonly ApiAccountingRepository _repo;

    public AccountingController(ApiAccountingRepository repo) => _repo = repo;

    /// <summary>Returns the P&amp;L summary (revenue, COGS, gross profit, expenses, net profit) for a date range.</summary>
    [HttpGet("summary")]
    public IActionResult GetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null)
    {
        var f = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var t = (to   ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

        try
        {
            var summary = _repo.GetSummary(f, t);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Returns expense transaction rows for a date range.</summary>
    [HttpGet("expenses")]
    public IActionResult GetExpenses(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to   = null)
    {
        var f = (from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
        var t = (to   ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

        try
        {
            var rows = _repo.GetExpenseRows(f, t);
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Returns all active expense categories (for populating dropdown on mobile).</summary>
    [HttpGet("expense-categories")]
    public IActionResult GetExpenseCategories()
    {
        try { return Ok(_repo.GetActiveCategories()); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>Records a new expense.</summary>
    [HttpPost("expenses")]
    public IActionResult AddExpense([FromBody] AddExpenseRequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });
        if (req.Amount <= 0)
            return BadRequest(new { error = "Amount must be greater than 0." });
        if (req.CategoryId <= 0)
            return BadRequest(new { error = "A valid category is required." });

        var username = User.Identity?.Name;
        try
        {
            _repo.AddExpense(req.CategoryId, req.Amount, req.Description, req.Date ?? DateTime.Today, username);
            return Ok(new { message = "Expense recorded." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record AddExpenseRequest(int CategoryId, decimal Amount, string? Description, DateTime? Date);
