using JaneERP.Api.Data;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]   // all authenticated roles can use tasks
public class TasksController : ControllerBase
{
    private readonly ApiTaskRepository _repo;
    private readonly CompanyContext    _ctx;

    public TasksController(ApiTaskRepository repo, CompanyContext ctx)
    {
        _repo = repo;
        _ctx  = ctx;
    }

    private string CurrentUser =>
        User.FindFirstValue(ClaimTypes.Name) ?? "mobile";

    // GET /api/tasks?status=Open&assignedTo=bob
    [HttpGet]
    public IActionResult GetTasks([FromQuery] string? status, [FromQuery] string? assignedTo)
        => Ok(_repo.GetTasks(status, assignedTo));

    // GET /api/tasks/{id}
    [HttpGet("{id:int}")]
    public IActionResult GetTask(int id)
    {
        var task = _repo.GetTask(id);
        return task == null ? NotFound() : Ok(task);
    }

    // POST /api/tasks
    [HttpPost]
    public IActionResult CreateTask([FromBody] CreateTaskRequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { error = "Title is required." });
        if (string.IsNullOrWhiteSpace(req.AssignedTo))
            return BadRequest(new { error = "AssignedTo is required." });

        var id = _repo.CreateTask(req, CurrentUser);
        return CreatedAtAction(nameof(GetTask), new { id }, new { taskId = id });
    }

    // PATCH /api/tasks/{id}/status
    [HttpPatch("{id:int}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] UpdateTaskStatusRequest req)
    {
        if (req == null) return BadRequest(new { error = "Request body required." });
        var valid = new[] { "Open", "In Progress", "Done" };
        if (!valid.Contains(req.Status))
            return BadRequest(new { error = $"Status must be one of: {string.Join(", ", valid)}" });

        var updated = _repo.UpdateStatus(id, req.Status);
        return updated ? Ok(new { success = true }) : NotFound();
    }

    // POST /api/tasks/{id}/comments
    [HttpPost("{id:int}/comments")]
    public IActionResult AddComment(int id, [FromBody] AddTaskCommentRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new { error = "Comment body is required." });

        _repo.AddComment(id, CurrentUser, req.Body);
        return Ok(new { success = true });
    }

    // GET /api/tasks/users  — list of usernames for assignee dropdown
    [HttpGet("users")]
    public IActionResult GetUsers() => Ok(_repo.GetUsernames());

    // ── Workflow endpoints ────────────────────────────────────────────────────

    /// <summary>List all task workflows.</summary>
    [HttpGet("workflows")]
    public IActionResult GetWorkflows()
    {
        var workflows = _repo.GetWorkflows()
            .Select(w => new { workflowId = w.WorkflowId, name = w.Name });
        return Ok(workflows);
    }

    /// <summary>List ordered stages for a workflow.</summary>
    [HttpGet("workflows/{id:int}/stages")]
    public IActionResult GetWorkflowStages(int id)
    {
        var stages = _repo.GetWorkflowStages(id)
            .Select(s => new { statusId = s.StatusId, statusName = s.StatusName, sortOrder = s.SortOrder });
        return Ok(stages);
    }

    /// <summary>Advance a task to the next workflow stage.</summary>
    [HttpPost("{id:int}/advance")]
    public IActionResult AdvanceStage(int id)
    {
        var success = _repo.AdvanceTaskStage(id, CurrentUser);
        if (!success)
            return BadRequest(new { error = "Cannot advance: already at final stage or no workflow set." });
        return Ok(new { success = true });
    }

    /// <summary>Set a specific workflow stage on a task.</summary>
    [HttpPost("{id:int}/stage")]
    public IActionResult SetStage(int id, [FromBody] SetStageRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Stage))
            return BadRequest(new { error = "Stage is required." });
        var success = _repo.SetTaskStage(id, req.Stage, CurrentUser);
        return success ? Ok(new { success = true }) : NotFound();
    }

    // ── Subtask endpoints ─────────────────────────────────────────────────────

    /// <summary>List subtasks (checklist items) for a task.</summary>
    [HttpGet("{id:int}/subtasks")]
    public IActionResult GetSubtasks(int id)
        => Ok(_repo.GetSubtasks(id));

    /// <summary>Add a subtask to a task.</summary>
    [HttpPost("{id:int}/subtasks")]
    public IActionResult AddSubtask(int id, [FromBody] AddSubtaskRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { error = "Title is required." });
        var success = _repo.AddSubtask(id, req.Title);
        return success ? Ok(new { success = true }) : BadRequest(new { error = "Failed to add subtask." });
    }

    /// <summary>Mark a subtask as complete.</summary>
    [HttpPost("{id:int}/subtasks/{subId:int}/complete")]
    public IActionResult CompleteSubtask(int id, int subId)
    {
        var success = _repo.CompleteSubtask(subId, CurrentUser);
        return success ? Ok(new { success = true }) : NotFound();
    }

    // ── History & linked record ───────────────────────────────────────────────

    /// <summary>Get the activity/audit log for a task.</summary>
    [HttpGet("{id:int}/history")]
    public IActionResult GetHistory(int id)
        => Ok(_repo.GetHistory(id));

    /// <summary>Get the linked module record for a task, if any.</summary>
    [HttpGet("{id:int}/linked")]
    public IActionResult GetLinkedRecord(int id)
    {
        var record = _repo.GetLinkedRecord(id);
        return record == null ? NotFound() : Ok(record);
    }
}

// ── Request models ────────────────────────────────────────────────────────────

public record SetStageRequest(string Stage);
public record AddSubtaskRequest(string Title);
