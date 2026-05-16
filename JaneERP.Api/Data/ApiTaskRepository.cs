using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiTaskRepository
{
    private readonly CompanyContext              _ctx;
    private readonly ILogger<ApiTaskRepository> _logger;

    public ApiTaskRepository(CompanyContext ctx, ILogger<ApiTaskRepository> logger)
    {
        _ctx    = ctx;
        _logger = logger;
    }

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public List<TaskItem> GetTasks(string? status, string? assignedTo,
        string? linkedModule = null, string? linkedId = null)
    {
        using var db = Connect();
        try
        {
            var conditions = new List<string>();
            if (status     != null) conditions.Add("t.Status = @status");
            if (assignedTo != null) conditions.Add("t.AssignedTo = @assignedTo");
            string joinClause = "";
            if (linkedModule != null && linkedId != null)
            {
                joinClause = "INNER JOIN TaskLinkedRecords lr ON lr.TaskId = t.TaskID AND lr.LinkedModule = @linkedModule AND lr.LinkedId = @linkedId";
            }
            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            return db.Query<TaskItem>($@"
                SELECT t.TaskID, t.Title, t.Description, t.AssignedTo, t.CreatedBy,
                       t.DueDate, t.Status, t.Priority, t.CreatedAt,
                       t.WorkflowCurrentStatus, t.Tags,
                       wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                {joinClause}
                {where}
                ORDER  BY t.DueDate, t.TaskID DESC",
                new { status, assignedTo, linkedModule, linkedId }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetTasks] Query failed"); return []; }
    }

    public TaskDetail? GetTask(int taskId)
    {
        using var db = Connect();
        try
        {
            var task = db.QueryFirstOrDefault<TaskDetail>(@"
                SELECT t.TaskID, t.Title, t.Description, t.AssignedTo, t.CreatedBy,
                       t.DueDate, t.Status, t.Priority, t.CreatedAt,
                       t.WorkflowCurrentStatus, t.Tags, t.WorkflowID,
                       wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                WHERE  t.TaskID = @taskId",
                new { taskId });
            if (task == null) return null;

            task.Comments = db.Query<TaskCommentItem>(@"
                SELECT CommentID, TaskID, Username, Body, CreatedAt
                FROM   TaskComments
                WHERE  TaskID = @taskId
                ORDER  BY CreatedAt",
                new { taskId }).ToList();

            task.Subtasks = db.Query<TaskSubtaskItem>(@"
                SELECT SubtaskId, Title, IsComplete, CompletedBy, CompletedAt
                FROM   TaskSubtasks
                WHERE  TaskId = @taskId
                ORDER  BY SortOrder",
                new { taskId }).ToList();

            task.LinkedRecords = db.Query<LinkedRecordItem>(@"
                SELECT LinkId, LinkedModule, LinkedId, LinkedDisplay, CreatedAt
                FROM   TaskLinkedRecords
                WHERE  TaskId = @taskId
                ORDER  BY CreatedAt",
                new { taskId }).ToList();

            task.History = db.Query<TaskHistoryItem>(@"
                SELECT FieldName, OldValue, NewValue, ChangedBy, ChangedAt
                FROM   TaskHistory
                WHERE  TaskId = @taskId
                ORDER  BY ChangedAt",
                new { taskId }).ToList();

            // Load workflow stages if workflow is assigned
            if (task.WorkflowID.HasValue && task.WorkflowID.Value != 0)
            {
                task.WorkflowStages = db.Query<string>(
                    "SELECT StatusName FROM TaskWorkflowStatuses WHERE WorkflowID = @wid ORDER BY SortOrder",
                    new { wid = task.WorkflowID.Value }).ToList();
            }

            return task;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetTask] Query failed for TaskID={Id}", taskId); return null; }
    }

    public int CreateTask(CreateTaskRequest req, string createdBy)
    {
        using var db = Connect();
        return db.QuerySingle<int>(@"
            INSERT INTO Tasks (Title, Description, AssignedTo, CreatedBy, DueDate, Status, Priority)
            VALUES (@Title, @Description, @AssignedTo, @CreatedBy, @DueDate, 'Open', @Priority);
            SELECT CAST(SCOPE_IDENTITY() AS INT)",
            new
            {
                req.Title,
                req.Description,
                req.AssignedTo,
                CreatedBy = createdBy,
                req.DueDate,
                req.Priority
            });
    }

    public bool UpdateStatus(int taskId, string status)
    {
        using var db = Connect();
        var rows = db.Execute(
            "UPDATE Tasks SET Status = @status WHERE TaskID = @taskId",
            new { taskId, status });
        return rows > 0;
    }

    public void AddComment(int taskId, string username, string body)
    {
        using var db = Connect();
        db.Execute(@"
            INSERT INTO TaskComments (TaskID, Username, Body)
            VALUES (@taskId, @username, @body)",
            new { taskId, username, body });

        // Parse @mentions and insert into TaskMentions
        var mentions = System.Text.RegularExpressions.Regex.Matches(body, @"@(\w+)")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var mentionedUser in mentions)
        {
            try
            {
                db.Execute(@"
                    INSERT INTO TaskMentions (TaskID, MentionedUser, MentionedBy, CommentText)
                    VALUES (@taskId, @mentionedUser, @mentionedBy, @body)",
                    new { taskId, mentionedUser, mentionedBy = username, body });
            }
            catch (Exception ex) { _logger.LogWarning(ex, "[ApiTaskRepository.AddComment] Failed to insert mention for {User}", mentionedUser); }
        }
    }

    public PagedTasksResponse GetPagedTasks(int page, int pageSize, string? status,
        string? assignedTo, string? search, string? tag)
    {
        using var db = Connect();
        try
        {
            var conditions = new List<string>();
            if (status     != null) conditions.Add("t.Status = @status");
            if (assignedTo != null) conditions.Add("t.AssignedTo = @assignedTo");
            if (!string.IsNullOrWhiteSpace(search))
                conditions.Add("(t.Title LIKE @search OR t.Description LIKE @search)");
            if (!string.IsNullOrWhiteSpace(tag))
                conditions.Add("t.Tags LIKE @tagSearch");

            var where       = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            var searchParam = $"%{search}%";
            var tagParam    = $"%{tag}%";
            var offset      = (page - 1) * pageSize;

            int total = db.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM Tasks t {where}",
                new { status, assignedTo, search = searchParam, tagSearch = tagParam });

            var items = db.Query<TaskItem>($@"
                SELECT t.TaskID, t.Title, t.Description, t.AssignedTo, t.CreatedBy,
                       t.DueDate, t.Status, t.Priority, t.CreatedAt,
                       t.WorkflowCurrentStatus, t.Tags,
                       wf.Name AS WorkflowName
                FROM   Tasks t
                LEFT JOIN TaskWorkflows wf ON wf.WorkflowID = t.WorkflowID
                {where}
                ORDER  BY t.DueDate, t.TaskID DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                new { status, assignedTo, search = searchParam, tagSearch = tagParam, offset, pageSize }).ToList();

            return new PagedTasksResponse { Items = items, Total = total, Page = page, PageSize = pageSize };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ApiTaskRepository.GetPagedTasks] Query failed");
            return new PagedTasksResponse { Items = [], Total = 0, Page = page, PageSize = pageSize };
        }
    }

    public List<LinkedRecordItem> GetLinkedRecords(int taskId)
    {
        using var db = Connect();
        try
        {
            return db.Query<LinkedRecordItem>(@"
                SELECT LinkId, LinkedModule, LinkedId, LinkedDisplay, CreatedAt
                FROM   TaskLinkedRecords
                WHERE  TaskId = @taskId
                ORDER  BY CreatedAt",
                new { taskId }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetLinkedRecords] Query failed for TaskID={Id}", taskId); return []; }
    }

    public bool AddLinkedRecord(int taskId, string module, string linkedId, string linkedDisplay)
    {
        using var db = Connect();
        try
        {
            int rows = db.Execute(@"
                INSERT INTO TaskLinkedRecords (TaskId, LinkedModule, LinkedId, LinkedDisplay)
                VALUES (@taskId, @module, @linkedId, @linkedDisplay)",
                new { taskId, module, linkedId, linkedDisplay });
            return rows > 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.AddLinkedRecord] Failed for TaskID={Id}", taskId); return false; }
    }

    public bool RemoveLinkedRecord(int linkId)
    {
        using var db = Connect();
        try
        {
            int rows = db.Execute("DELETE FROM TaskLinkedRecords WHERE LinkId = @linkId", new { linkId });
            return rows > 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.RemoveLinkedRecord] Failed for LinkID={Id}", linkId); return false; }
    }

    public List<string> GetUsernames()
    {
        using var db = Connect();
        try { return db.Query<string>("SELECT Username FROM Users ORDER BY Username").ToList(); }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetUsernames] Query failed"); return []; }
    }

    // ── Workflows ─────────────────────────────────────────────────────────────

    public List<(int WorkflowId, string Name)> GetWorkflows()
    {
        using var db = Connect();
        try
        {
            var rows = db.Query<WorkflowRow>(
                "SELECT WorkflowID, Name FROM TaskWorkflows ORDER BY Name").ToList();
            return rows.Select(r => (r.WorkflowID, r.Name)).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetWorkflows] Query failed"); return []; }
    }

    public List<(int StatusId, string StatusName, int SortOrder)> GetWorkflowStages(int workflowId)
    {
        using var db = Connect();
        try
        {
            var rows = db.Query<WorkflowStageRow>(
                "SELECT StatusID, StatusName, SortOrder FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId ORDER BY SortOrder",
                new { workflowId }).ToList();
            return rows.Select(r => (r.StatusID, r.StatusName, r.SortOrder)).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetWorkflowStages] Query failed for WorkflowID={Id}", workflowId); return []; }
    }

    public bool AdvanceTaskStage(int taskId, string advancedBy)
    {
        using var db = Connect();
        try
        {
            var task = db.QueryFirstOrDefault<TaskWorkflowInfo>(
                "SELECT WorkflowID, WorkflowCurrentStatus FROM Tasks WHERE TaskID = @taskId",
                new { taskId });
            if (task == null || task.WorkflowID == null) return false;

            var stages = db.Query<WorkflowStageRow>(
                "SELECT StatusID, StatusName, SortOrder FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId ORDER BY SortOrder",
                new { workflowId = task.WorkflowID }).ToList();
            if (stages.Count == 0) return false;

            var current = stages.FirstOrDefault(s => s.StatusName == task.WorkflowCurrentStatus);
            int currentSort = current?.SortOrder ?? -1;

            var next = stages.Where(s => s.SortOrder > currentSort).OrderBy(s => s.SortOrder).FirstOrDefault();
            if (next == null) return false; // already on final stage

            bool isFinalStage = !stages.Any(s => s.SortOrder > next.SortOrder);

            db.Execute(@"
                UPDATE Tasks
                SET WorkflowCurrentStatus = @nextStatus
                    , Status = CASE WHEN @isFinal = 1 THEN 'Done' ELSE Status END
                WHERE TaskID = @taskId",
                new { taskId, nextStatus = next.StatusName, isFinal = isFinalStage ? 1 : 0 });

            db.Execute(@"
                INSERT INTO TaskHistory (TaskId, FieldName, OldValue, NewValue, ChangedBy)
                VALUES (@taskId, 'WorkflowStage', @oldValue, @newValue, @changedBy)",
                new { taskId, oldValue = task.WorkflowCurrentStatus, newValue = next.StatusName, changedBy = advancedBy });

            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.AdvanceTaskStage] Failed for TaskID={Id}", taskId); return false; }
    }

    public bool SetTaskStage(int taskId, string stageName, string changedBy)
    {
        using var db = Connect();
        try
        {
            var task = db.QueryFirstOrDefault<TaskWorkflowInfo>(
                "SELECT WorkflowID, WorkflowCurrentStatus FROM Tasks WHERE TaskID = @taskId",
                new { taskId });
            if (task == null || task.WorkflowID == null) return false;

            var stage = db.QueryFirstOrDefault<WorkflowStageRow>(
                "SELECT StatusID, StatusName, SortOrder FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId AND StatusName = @stageName",
                new { workflowId = task.WorkflowID, stageName });
            if (stage == null) return false;

            // Check if this is the final stage
            bool isFinalStage = !db.ExecuteScalar<bool>(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM TaskWorkflowStatuses WHERE WorkflowID = @workflowId AND SortOrder > @sort) THEN 1 ELSE 0 END",
                new { workflowId = task.WorkflowID, sort = stage.SortOrder });

            db.Execute(@"
                UPDATE Tasks
                SET WorkflowCurrentStatus = @stageName
                    , Status = CASE WHEN @isFinal = 1 THEN 'Done' ELSE Status END
                WHERE TaskID = @taskId",
                new { taskId, stageName, isFinal = isFinalStage ? 1 : 0 });

            db.Execute(@"
                INSERT INTO TaskHistory (TaskId, FieldName, OldValue, NewValue, ChangedBy)
                VALUES (@taskId, 'WorkflowStage', @oldValue, @newValue, @changedBy)",
                new { taskId, oldValue = task.WorkflowCurrentStatus, newValue = stageName, changedBy });

            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.SetTaskStage] Failed for TaskID={Id}", taskId); return false; }
    }

    // ── Subtasks ──────────────────────────────────────────────────────────────

    public List<object> GetSubtasks(int taskId)
    {
        using var db = Connect();
        try
        {
            return db.Query(@"
                SELECT SubtaskId, Title, IsComplete, CompletedBy, CompletedAt
                FROM   TaskSubtasks
                WHERE  TaskId = @taskId
                ORDER BY SortOrder",
                new { taskId })
                .Select(r => (object)new
                {
                    r.SubtaskId,
                    r.Title,
                    IsComplete  = (bool)r.IsComplete,
                    r.CompletedBy,
                    r.CompletedAt
                }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetSubtasks] Query failed for TaskID={Id}", taskId); return []; }
    }

    public bool CompleteSubtask(int subtaskId, string completedBy)
    {
        using var db = Connect();
        try
        {
            int rows = db.Execute(@"
                UPDATE TaskSubtasks
                SET IsComplete = 1, CompletedBy = @completedBy, CompletedAt = GETDATE()
                WHERE SubtaskId = @subtaskId",
                new { subtaskId, completedBy });
            return rows > 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.CompleteSubtask] Failed for SubtaskID={Id}", subtaskId); return false; }
    }

    public bool AddSubtask(int taskId, string title)
    {
        using var db = Connect();
        try
        {
            int sortOrder = db.ExecuteScalar<int>(
                "SELECT ISNULL(MAX(SortOrder), -1) + 1 FROM TaskSubtasks WHERE TaskId = @taskId",
                new { taskId });
            int rows = db.Execute(@"
                INSERT INTO TaskSubtasks (TaskId, Title, SortOrder)
                VALUES (@taskId, @title, @sortOrder)",
                new { taskId, title, sortOrder });
            return rows > 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.AddSubtask] Failed for TaskID={Id}", taskId); return false; }
    }

    // ── Linked Record ─────────────────────────────────────────────────────────

    public object? GetLinkedRecord(int taskId)
    {
        using var db = Connect();
        try
        {
            var row = db.QueryFirstOrDefault(@"
                SELECT LinkedModule, LinkedId, LinkedDisplay
                FROM   TaskLinkedRecords
                WHERE  TaskId = @taskId",
                new { taskId });
            if (row == null) return null;
            return new
            {
                row.LinkedModule,
                row.LinkedId,
                row.LinkedDisplay
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetLinkedRecord] Query failed for TaskID={Id}", taskId); return null; }
    }

    // ── History ───────────────────────────────────────────────────────────────

    public List<object> GetHistory(int taskId)
    {
        using var db = Connect();
        try
        {
            return db.Query(@"
                SELECT FieldName, OldValue, NewValue, ChangedBy, ChangedAt
                FROM   TaskHistory
                WHERE  TaskId = @taskId
                ORDER BY ChangedAt",
                new { taskId })
                .Select(r => (object)new
                {
                    r.FieldName,
                    r.OldValue,
                    r.NewValue,
                    r.ChangedBy,
                    r.ChangedAt
                }).ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "[ApiTaskRepository.GetHistory] Query failed for TaskID={Id}", taskId); return []; }
    }

    // ── Private helper types ──────────────────────────────────────────────────

    private class WorkflowRow
    {
        public int    WorkflowID { get; set; }
        public string Name       { get; set; } = "";
    }

    private class WorkflowStageRow
    {
        public int    StatusID   { get; set; }
        public string StatusName { get; set; } = "";
        public int    SortOrder  { get; set; }
    }

    private class TaskWorkflowInfo
    {
        public int?   WorkflowID            { get; set; }
        public string? WorkflowCurrentStatus { get; set; }
    }
}
